using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Engines;

/// <summary>
/// Rotate right and scale in percent on video, on real files: sizes by the order of the actions, a marker in the
/// corner proves the picture is really turned, the frame size of the video settings goes first, video to sound still refuses.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegVideoOperationsTests : IClassFixture<VideoSourcesFixture>, IDisposable
{
    private readonly VideoSourcesFixture _fixture;
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly FfmpegVideoEngine _engine = new();

    public FfmpegVideoOperationsTests(VideoSourcesFixture fixture)
    {
        _fixture = fixture;
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    private static ToolStatus Ffmpeg()
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg);
    }

    private async Task<string> ConvertAsync(string sourcePath, FileFormat to, ConversionOptions options)
    {
        await using FileStream input = File.OpenRead(sourcePath);
        using MemoryStream output = new();
        FileFormat from = Sniff.Match(await File.ReadAllBytesAsync(sourcePath));
        ConversionRequest request = new(input, output, from, to, options, Ffmpeg(), _temp, sourcePath, null);

        await _engine.ConvertAsync(request, CancellationToken.None);

        string path = Path.Combine(_temp, $"result-{Guid.NewGuid():N}.{to.Extension}");
        await File.WriteAllBytesAsync(path, output.ToArray());
        return path;
    }

    private async Task<string> SizeAsync(string path)
    {
        string width = await _fixture.ProbeAsync(path, "stream=width", "v:0");
        string height = await _fixture.ProbeAsync(path, "stream=height", "v:0");
        return $"{width}x{height}";
    }

    // First frame as RGB bytes: the marker is looked up by colour, so a lossy codec is no problem.
    private async Task<(int Width, int Height, byte[] Rgb)> FirstFrameAsync(string path)
    {
        string[] size = (await SizeAsync(path)).Split('x');
        int width = int.Parse(size[0]);
        int height = int.Parse(size[1]);
        using MemoryStream output = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            _fixture.Ffmpeg,
            ["-v", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"],
            StandardOutput: output));
        Assert.Equal(0, result.ExitCode);
        return (width, height, output.ToArray());
    }

    private static bool IsRed(byte[] rgb, int width, int x, int y)
    {
        int at = ((y * width) + x) * 3;
        return rgb[at] > 200 && rgb[at + 1] < 90 && rgb[at + 2] < 90;
    }

    private static ConversionOptions With(params Operation[] operations)
    {
        ConversionOptions options = new();
        foreach (Operation operation in operations)
        {
            options.Operations.Add(operation);
        }

        return options;
    }

    private async Task<string> MarkedSourceAsync()
    {
        string path = Path.Combine(_temp, "marked.mp4");
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            _fixture.Ffmpeg,
            [
                "-v", "error", "-y", "-f", "lavfi", "-i", "color=c=blue:size=640x480:rate=10:duration=1",
                "-vf", "drawbox=x=0:y=0:w=60:h=60:color=red:t=fill",
                "-c:v", "libx264", "-crf", "12", "-pix_fmt", "yuv420p", path,
            ]));
        Assert.Equal(0, result.ExitCode);
        return path;
    }

    [Fact]
    public void TheVideoEngine_TakesActions_AndTheCatalogStillRefusesThemForSound()
    {
        Assert.True(_engine.SupportsOperations);
        Assert.False(new FfmpegAudioEngine().SupportsOperations);
    }

    [ExternalTheory]
    [InlineData("rotate", "480x640")]
    [InlineData("scale50", "320x240")]
    [InlineData("rotate,scale75", "360x480")]
    [InlineData("rotate,scale75,scale75", "270x360")]
    [InlineData("scale33", "210x158")]
    [InlineData("scale1", "6x4")]
    [InlineData("rotate,rotate", "640x480")]
    public async Task Sizes_FollowTheActionsInOrder_RoundedDownToEven(string actions, string expected)
    {
        Operation[] operations = [.. actions.Split(',').Select(a => a switch
        {
            "rotate" => Operation.RotateRight,
            _ => Operation.ScalePercent(int.Parse(a["scale".Length..])),
        })];

        string path = await ConvertAsync(_fixture.Rich("mp4"), FileFormat.Mp4, With(operations));

        Assert.Equal(expected, await SizeAsync(path));
    }

    [ExternalFact]
    public async Task RotateRight_TurnsThePictureClockwise_TheCornerMarkMovesFromTopLeftToTopRight()
    {
        string source = await MarkedSourceAsync();

        string path = await ConvertAsync(source, FileFormat.Mp4, With(Operation.RotateRight));

        (int width, int height, byte[] rgb) = await FirstFrameAsync(path);
        Assert.Equal((480, 640), (width, height));
        Assert.True(IsRed(rgb, width, width - 20, 20), "the mark is not in the top right corner");
        Assert.False(IsRed(rgb, width, 20, 20), "the top left corner is still marked");
    }

    [ExternalFact]
    public async Task ScaleBeforeRotate_AndFrameSizeOfTheSettings_GoFirst()
    {
        ConversionOptions options = With(Operation.RotateRight);
        options.Video = new VideoOptions { Width = 320 };

        string path = await ConvertAsync(_fixture.Rich("mp4"), FileFormat.Mp4, options);

        // 640x480 becomes 320x240 by the settings, then the rotation turns it to 240x320.
        Assert.Equal("240x320", await SizeAsync(path));
    }

    [ExternalFact]
    public async Task Actions_WorkForEveryContainer()
    {
        foreach (FileFormat to in new[] { FileFormat.Mkv, FileFormat.Webm, FileFormat.Mov, FileFormat.Avi })
        {
            string path = await ConvertAsync(_fixture.Rich("mp4"), to, With(Operation.RotateRight, Operation.ScalePercent(50)));

            Assert.Equal("240x320", await SizeAsync(path));
        }
    }

    [ExternalFact]
    public async Task ThroughTheConverter_VideoToVideoWithActions_Works_AndVideoToSoundStillRefuses()
    {
        Converter converter = FfmpegConverter.Create();
        ConversionOptions options = With(Operation.ScalePercent(50));

        ConversionResult result = await converter.ConvertAsync(Source.File(_fixture.Rich("mkv")), Target.Memory(FileFormat.Mp4), options);
        ConversionException refused = await Assert.ThrowsAsync<ConversionException>(() =>
            converter.ConvertAsync(Source.File(_fixture.Rich("mkv")), Target.Memory(FileFormat.Mp3), options));

        Assert.True(result.Succeeded);
        Assert.True(result.Reencoded);
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, refused.Reason);
    }
}
