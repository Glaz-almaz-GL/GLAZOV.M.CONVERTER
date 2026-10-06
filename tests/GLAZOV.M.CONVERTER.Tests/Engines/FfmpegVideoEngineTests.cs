using System.Globalization;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Engines;

/// <summary>
/// Video to video through FFmpeg on real files: all 25 pairs read back by ffprobe, codecs by the defaults table,
/// frame size, frame rate, quality and bitrate, missing tracks, a missing encoder, progress and cancellation.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegVideoEngineTests : IClassFixture<VideoSourcesFixture>, IDisposable
{
    private static readonly (string Name, FileFormat Format, string Video, string Audio)[] Containers =
    [
        ("mp4", FileFormat.Mp4, "h264", "aac"),
        ("mkv", FileFormat.Mkv, "h264", "aac"),
        ("webm", FileFormat.Webm, "vp9", "opus"),
        ("mov", FileFormat.Mov, "h264", "aac"),
        ("avi", FileFormat.Avi, "mpeg4", "mp3"),
    ];

    private readonly VideoSourcesFixture _fixture;
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly FfmpegVideoEngine _engine = new();

    public FfmpegVideoEngineTests(VideoSourcesFixture fixture)
    {
        _fixture = fixture;
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    public static IEnumerable<object[]> AllPairs()
    {
        foreach ((string from, _, _, _) in Containers)
        {
            foreach ((string to, _, _, _) in Containers)
            {
                yield return new object[] { from, to };
            }
        }
    }

    private static ToolStatus Ffmpeg()
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg);
    }

    private static (FileFormat Format, string Video, string Audio) Container(string name)
    {
        (_, FileFormat format, string video, string audio) = Containers.Single(c => c.Name == name);
        return (format, video, audio);
    }

    private async Task<byte[]> ConvertAsync(
        string sourcePath,
        FileFormat to,
        ConversionOptions? options = null,
        bool withPath = true,
        Action<int>? onProgress = null,
        CancellationToken cancellationToken = default,
        ToolStatus? tool = null)
    {
        await using FileStream input = File.OpenRead(sourcePath);
        using MemoryStream output = new();
        FileFormat from = Sniff.Match(await File.ReadAllBytesAsync(sourcePath));
        ConversionRequest request = new(
            input, output, from, to, options ?? new ConversionOptions(), tool ?? Ffmpeg(), _temp, withPath ? sourcePath : null, onProgress);

        await _engine.ConvertAsync(request, cancellationToken);

        return output.ToArray();
    }

    private async Task<string> SaveAsync(byte[] bytes, FileFormat format)
    {
        string path = Path.Combine(_temp, $"result-{Guid.NewGuid():N}.{format.Extension}");
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    private static double Number(string text)
    {
        return double.Parse(text, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Pairs_AreTwentyFive_AndRegisteredInTheBuiltInSet()
    {
        Assert.Equal(25, _engine.Pairs.Count);
        Assert.Contains(new ConversionPair(FileFormat.Mp4, FileFormat.Mp4), _engine.Pairs);
        Assert.Contains(new ConversionPair(FileFormat.Avi, FileFormat.Webm), _engine.Pairs);
        Assert.DoesNotContain(new ConversionPair(FileFormat.Mp4, FileFormat.Mp3), _engine.Pairs);
        Assert.Equal("ffmpeg", Assert.Single(_engine.Tools, t => t.Required).Name);
        Assert.False(Assert.Single(_engine.Tools, t => t.Name == "ffprobe").Required);
        Assert.True(_engine.SupportsOperations);
        Assert.Contains(new Converter().Capabilities.All, c => c.From.Equals(FileFormat.Mkv) && c.To.Equals(FileFormat.Webm));
    }

    [ExternalTheory]
    [MemberData(nameof(AllPairs))]
    public async Task EveryVideoPair_ReadsBack_WithTheCodecsOfTheDefaultsTable(string from, string to)
    {
        (FileFormat format, string videoCodec, string audioCodec) = Container(to);
        string sourcePath = _fixture.Source(from);

        byte[] bytes = await ConvertAsync(sourcePath, format);
        string resultPath = await SaveAsync(bytes, format);

        Assert.Same(format, Sniff.Match(bytes));
        Assert.Equal(videoCodec, await _fixture.ProbeAsync(resultPath, "stream=codec_name", "v:0"));
        Assert.Equal(audioCodec, await _fixture.ProbeAsync(resultPath, "stream=codec_name", "a:0"));
        double expected = Number(await _fixture.ProbeAsync(sourcePath, "format=duration"));
        double actual = Number(await _fixture.ProbeAsync(resultPath, "format=duration"));
        Assert.InRange(actual, expected - 0.3, expected + 0.3);
        Assert.Empty(Directory.GetFiles(_temp, "convert-*"));
        Assert.Empty(Directory.GetFiles(_temp, "input-*"));
    }

    [ExternalTheory]
    [InlineData(320, null, "320x240")]
    [InlineData(321, null, "320x240")]
    [InlineData(null, 200, "266x200")]
    [InlineData(321, 241, "320x240")]
    [InlineData(100, 50, "100x50")]
    [InlineData(1, 1, "2x2")]
    public async Task FrameSize_KeepsProportionsWithOneSide_AndRoundsDownToEven(int? width, int? height, string expected)
    {
        ConversionOptions options = new() { Video = new VideoOptions { Width = width, Height = height } };

        string path = await SaveAsync(await ConvertAsync(_fixture.Rich("mp4"), FileFormat.Mp4, options), FileFormat.Mp4);

        Assert.Equal(expected, await Size(path));
    }

    private async Task<string> Size(string path)
    {
        string width = await _fixture.ProbeAsync(path, "stream=width", "v:0");
        string height = await _fixture.ProbeAsync(path, "stream=height", "v:0");
        return $"{width}x{height}";
    }

    [ExternalFact]
    public async Task FrameRate_IsApplied()
    {
        ConversionOptions options = new() { Video = new VideoOptions { FrameRate = 5 } };

        string path = await SaveAsync(await ConvertAsync(_fixture.Rich("mp4"), FileFormat.Mp4, options), FileFormat.Mp4);

        Assert.Equal("5/1", await _fixture.ProbeAsync(path, "stream=r_frame_rate", "v:0"));
    }

    [ExternalTheory]
    [InlineData("mp4")]
    [InlineData("webm")]
    [InlineData("avi")]
    public async Task LowQuality_GivesAMuchSmallerFile_ThanHighQuality(string container)
    {
        (FileFormat format, _, _) = Container(container);

        byte[] low = await ConvertAsync(_fixture.Noisy, format, new ConversionOptions { Video = new VideoOptions { Quality = 10 } });
        byte[] high = await ConvertAsync(_fixture.Noisy, format, new ConversionOptions { Video = new VideoOptions { Quality = 90 } });

        Assert.True(low.Length * 1.5 < high.Length, $"Quality 10 gave {low.Length} bytes, quality 90 — {high.Length}");
    }

    [ExternalTheory]
    [InlineData("mp4", 500_000)]
    [InlineData("mp4", 2_000_000)]
    [InlineData("webm", 500_000)]
    [InlineData("avi", 500_000)]
    public async Task Bitrate_IsKeptWithinThirtyPercent_OnDemandingPicture(string container, int bitrate)
    {
        (FileFormat format, _, _) = Container(container);
        ConversionOptions options = new() { Video = new VideoOptions { Bitrate = bitrate, Quality = 90 } };

        string path = await SaveAsync(await ConvertAsync(_fixture.Noisy, format, options), format);

        double actual = Number(await _fixture.ProbeAsync(path, "format=bit_rate", "v:0"));
        Assert.InRange(actual, bitrate * 0.7, bitrate * 1.3);
    }

    [ExternalFact]
    public async Task AudioOptions_ApplyToTheSoundTrackOfTheVideo()
    {
        ConversionOptions options = new() { Audio = new AudioOptions { SampleRate = 22050, Channels = 2 } };

        string path = await SaveAsync(await ConvertAsync(_fixture.Rich("mp4"), FileFormat.Mkv, options), FileFormat.Mkv);

        Assert.Equal("22050", await _fixture.ProbeAsync(path, "stream=sample_rate", "a:0"));
        Assert.Equal("2", await _fixture.ProbeAsync(path, "stream=channels", "a:0"));
    }

    [ExternalFact]
    public async Task InputWithoutSound_GivesAResultWithoutSound()
    {
        string path = await SaveAsync(await ConvertAsync(_fixture.NoSound, FileFormat.Webm), FileFormat.Webm);

        Assert.Equal("vp9", await _fixture.ProbeAsync(path, "stream=codec_name", "v:0"));
        Assert.Equal(string.Empty, await _fixture.ProbeAsync(path, "stream=codec_name", "a:0"));
    }

    [ExternalFact]
    public async Task InputWithoutPicture_FailsWithTheLastLinesOfFfmpeg()
    {
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() => ConvertAsync(_fixture.NoPicture, FileFormat.Mp4));

        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Contains("matches no streams", ex.ToolOutput);
        Assert.EndsWith("в файле нет видеодорожки", ex.Message);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalTheory]
    [InlineData("mp4", "mkv")]
    [InlineData("webm", "avi")]
    public async Task StreamInputAndPathInput_GiveTheSameResult(string from, string to)
    {
        (FileFormat format, _, _) = Container(to);

        byte[] byPath = await ConvertAsync(_fixture.Rich(from), format, withPath: true);
        byte[] byStream = await ConvertAsync(_fixture.Rich(from), format, withPath: false);

        Assert.Equal(byPath.Length, byStream.Length);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task Progress_GrowsAndStaysBelow100_OnALongFile()
    {
        List<int> percents = [];
        using CancellationTokenSource enough = new();

        // Three growing values are proof enough; the rest of the long file is not encoded.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConvertAsync(_fixture.Long, FileFormat.Mp4, onProgress: percent =>
        {
            lock (percents)
            {
                percents.Add(percent);
                if (percents.Count >= 3)
                {
                    enough.Cancel();
                }
            }
        }, cancellationToken: enough.Token));

        Assert.True(percents.Count >= 2, $"Too few percents: {string.Join(", ", percents)}");
        Assert.Equal(percents.Order().ToArray(), percents);
        Assert.Equal(percents.Distinct().Count(), percents.Count);
        Assert.InRange(percents[^1], 1, 99);
    }

    [ExternalFact]
    public async Task CancelInTheMiddle_StopsTheWork_AndLeavesNoTempFiles()
    {
        using CancellationTokenSource cts = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ConvertAsync(_fixture.Long, FileFormat.Mp4, withPath: false, onProgress: _ => cts.Cancel(), cancellationToken: cts.Token));

        Assert.Empty(Directory.GetFiles(_temp));
    }

    [Fact]
    public async Task MissingEncoder_IsExplainedInPlainWords()
    {
        string script = Path.Combine(_temp, OperatingSystem.IsWindows() ? "fake-ffmpeg.cmd" : "fake-ffmpeg.sh");
        await File.WriteAllTextAsync(script, OperatingSystem.IsWindows()
            ? "@echo off\r\necho Unknown encoder 'libx264' 1>&2\r\nexit /b 1\r\n"
            : "#!/bin/sh\necho \"Unknown encoder 'libx264'\" >&2\nexit 1\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        ToolStatus fake = new("ffmpeg", found: true, script, string.Empty);
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() =>
            ConvertAsync(Path.Combine(AppContext.BaseDirectory, "Samples", "sample.mp4"), FileFormat.Mp4, tool: fake));

        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Contains("ваш FFmpeg собран без кодека libx264", ex.Message);
        Assert.Empty(Directory.GetFiles(_temp, "convert-*"));
    }

    [ExternalFact]
    public async Task ThroughTheConverter_VideoToVideoWithReencode_ReportsReencoded()
    {
        Converter converter = new();

        ConversionOptions options = new() { Video = new VideoOptions { Reencode = true } };

        ConversionResult result = await converter.ConvertAsync(Source.File(_fixture.Source("mkv")), Target.Memory(FileFormat.Mp4), options);

        Assert.True(result.Succeeded);
        Assert.Same(FileFormat.Mkv, result.SourceType);
        Assert.True(result.Reencoded);
        Assert.Same(FileFormat.Mp4, Sniff.Match(result.Bytes!));
    }
}
