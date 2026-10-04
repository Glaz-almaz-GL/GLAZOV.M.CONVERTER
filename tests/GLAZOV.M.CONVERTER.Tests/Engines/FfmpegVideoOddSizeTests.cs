using System.Diagnostics;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Engines;

/// <summary>
/// A source whose frame has an odd width or height must still convert to H.264 containers without a size option.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegVideoOddSizeTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convodd-" + Guid.NewGuid().ToString("N"));

    public FfmpegVideoOddSizeTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    [ExternalTheory]
    [InlineData("mp4")]
    [InlineData("mkv")]
    [InlineData("mov")]
    [InlineData("webm")]
    [InlineData("avi")]
    public async Task OddSizedSource_ConvertsToEveryContainer_WithoutSizeOptions(string to)
    {
        ToolStatus tool = new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate("ffmpeg");
        string source = Path.Combine(_temp, "odd.mp4");
        // libx264 cannot write an odd frame, so the source is made by the encoder that can (mpeg4 in mp4 keeps 641x481).
        using (Process make = Process.Start(new ProcessStartInfo(tool.Path!, $"-v error -y -f lavfi -i testsrc=size=641x481:rate=25:duration=1 -c:v mpeg4 \"{source}\"") { CreateNoWindow = true })!)
        {
            await make.WaitForExitAsync();
            Assert.Equal(0, make.ExitCode);
        }

        FileFormat target = to switch
        {
            "mp4" => FileFormat.Mp4,
            "mkv" => FileFormat.Mkv,
            "mov" => FileFormat.Mov,
            "webm" => FileFormat.Webm,
            _ => FileFormat.Avi,
        };
        await using FileStream input = File.OpenRead(source);
        using MemoryStream output = new();
        ConversionRequest request = new(
            input, output, SignatureMatcher.Match(await File.ReadAllBytesAsync(source)), target, new ConversionOptions(), tool, _temp, source, null);

        await new FfmpegVideoEngine().ConvertAsync(request, CancellationToken.None);

        Assert.True(output.Length > 0);
    }
}
