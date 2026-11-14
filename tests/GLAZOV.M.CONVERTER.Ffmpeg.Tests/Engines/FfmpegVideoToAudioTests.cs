using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Formats.Options;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;
using System.Globalization;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Engines;

/// <summary>
/// Sound out of a video input through the audio engine, on real files made by FFmpeg: all 25 pairs read back by ffprobe,
/// stream and path input agree, no sound is an understandable failure, progress grows, cancellation leaves nothing behind.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegVideoToAudioTests : IClassFixture<VideoSourcesFixture>, IDisposable
{
    private static readonly (string Name, FileFormat Format, string Codec)[] Targets =
    [
        ("mp3", FileFormat.Mp3, "mp3"),
        ("wav", FileFormat.Wav, "pcm_s16le"),
        ("ogg", FileFormat.Ogg, "vorbis"),
        ("flac", FileFormat.Flac, "flac"),
        ("m4a", FileFormat.M4a, "aac"),
    ];

    private readonly VideoSourcesFixture _fixture;
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly FfmpegAudioEngine _engine = new();

    public FfmpegVideoToAudioTests(VideoSourcesFixture fixture)
    {
        _fixture = fixture;
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    public static TheoryData<string, string> AllPairs()
    {
        TheoryData<string, string> data = [];
        foreach (string source in (string[])["mp4", "mkv", "webm", "mov", "avi"])
        {
            foreach ((string name, _, _) in Targets)
            {
                data.Add(source, name);
            }
        }

        return data;
    }

    private static ToolStatus Ffmpeg()
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg);
    }

    private static (FileFormat Format, string Codec) TargetOf(string name)
    {
        (_, FileFormat format, string codec) = Targets.Single(t => t.Name == name);
        return (format, codec);
    }

    private async Task<byte[]> ConvertAsync(string sourcePath, FileFormat to, bool withPath, Action<int>? onProgress = null, CancellationToken cancellationToken = default)
    {
        await using FileStream input = File.OpenRead(sourcePath);
        using MemoryStream output = new();
        FileFormat from = Sniff.Match(await File.ReadAllBytesAsync(sourcePath, cancellationToken));
        ConversionRequest request = new(input, output, from, to, new ConversionOptions(), Ffmpeg(), _temp)
        {
            InputPath = withPath ? sourcePath : null,
            OnProgress = onProgress,
        };

        await _engine.ConvertAsync(request, cancellationToken);

        return output.ToArray();
    }

    [Fact]
    public void VideoPairs_AreInTheCatalogOfTheEngine()
    {
        FileFormat[] videos = [FileFormat.Mp4, FileFormat.Mkv, FileFormat.Webm, FileFormat.Mov, FileFormat.Avi];

        Assert.All(videos, v => Assert.All(Targets, t => Assert.Contains(new ConversionPair(v, t.Format), _engine.Pairs)));
        Assert.DoesNotContain(new ConversionPair(FileFormat.Mp3, FileFormat.Mp4), _engine.Pairs);
    }

    [ExternalTheory]
    [MemberData(nameof(AllPairs))]
    public async Task EveryVideoToAudioPair_ReadsBack_WithTheRightCodecAndTheSameDuration(string source, string target)
    {
        (FileFormat format, string codec) = TargetOf(target);
        string sourcePath = _fixture.Source(source);

        byte[] bytes = await ConvertAsync(sourcePath, format, withPath: true);
        string resultPath = Path.Combine(_temp, "result." + format.Extension);
        await File.WriteAllBytesAsync(resultPath, bytes);

        Assert.Same(format, Sniff.Match(bytes));
        Assert.Equal(codec, await _fixture.ProbeAsync(resultPath, "stream=codec_name"));
        double expected = double.Parse(await _fixture.ProbeAsync(sourcePath, "format=duration"), CultureInfo.InvariantCulture);
        double actual = double.Parse(await _fixture.ProbeAsync(resultPath, "format=duration"), CultureInfo.InvariantCulture);
        Assert.InRange(actual, expected - 0.1, expected + 0.1);
        Assert.Empty(Directory.GetFiles(_temp, "convert-*"));
        Assert.Empty(Directory.GetFiles(_temp, "input-*"));
    }

    [ExternalTheory]
    [InlineData("mp4", "mp3")]
    [InlineData("mkv", "wav")]
    [InlineData("webm", "ogg")]
    [InlineData("mov", "flac")]
    [InlineData("avi", "m4a")]
    public async Task StreamInputAndPathInput_GiveTheSameResult(string source, string target)
    {
        (FileFormat format, _) = TargetOf(target);
        string sourcePath = _fixture.Source(source);

        byte[] byPath = await ConvertAsync(sourcePath, format, withPath: true);
        byte[] byStream = await ConvertAsync(sourcePath, format, withPath: false);

        // An Ogg stream carries a random serial number, so only its size can be equal.
        if (target == "ogg")
        {
            Assert.Equal(byPath.Length, byStream.Length);
        }
        else
        {
            Assert.Equal(byPath, byStream);
        }

        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task VideoWithoutSound_FailsWithAnUnderstandableToolError()
    {
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() => ConvertAsync(_fixture.NoSound, FileFormat.Mp3, withPath: true));

        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Contains("matches no streams", ex.ToolOutput);
        Assert.EndsWith("в файле нет звуковой дорожки", ex.Message);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task Progress_GrowsAndStaysBelow100_OnALongFile()
    {
        List<int> percents = [];

        await ConvertAsync(_fixture.Long, FileFormat.Flac, withPath: true, percent =>
        {
            lock (percents)
            {
                percents.Add(percent);
            }
        });

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
            ConvertAsync(_fixture.Long, FileFormat.Flac, withPath: false, _ => cts.Cancel(), cts.Token));

        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task OperationsOnVideoToAudio_AreRefusedBeforeAnyWork()
    {
        Converter converter = FfmpegConverter.Create();
        ConversionOptions options = new() { Operations = { VisualOperations.RotateRight } };

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(() =>
            converter.ConvertAsync(Source.File(_fixture.Source("mp4")), Target.Memory(FileFormat.Mp3), options));

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
    }

    [ExternalFact]
    public async Task ThroughTheConverter_VideoFileToMp3_ReportsReencodedAndReadsBack()
    {
        Converter converter = FfmpegConverter.Create();

        ConversionResult result = await converter.ConvertAsync(Source.File(_fixture.Source("mkv")), Target.Memory(FileFormat.Mp3));

        Assert.True(result.Succeeded);
        Assert.Same(FileFormat.Mkv, result.SourceType);
        Assert.True(result.Reencoded);
        Assert.Same(FileFormat.Mp3, Sniff.Match(result.Bytes!));
    }
}
