using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Engines;

/// <summary>
/// Звуковой движок на настоящем FFmpeg: девять пар форматов, параметры звука, испорченный вход, чистота временной папки.
/// Живые проверки пропускаются (не проходят!), если FFmpeg на машине нет.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegAudioEngineTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly FfmpegAudioEngine _engine = new();

    public FfmpegAudioEngineTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    private static byte[] Sample(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));
    }

    private static ToolStatus Ffmpeg()
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate("ffmpeg");
    }

    private async Task<byte[]> Convert(byte[] input, FileFormat to, AudioOptions? audio = null)
    {
        using MemoryStream source = new(input);
        using MemoryStream output = new();

        await _engine.ConvertAsync(new EngineRequest(source, output, to, audio, Ffmpeg(), _temp), CancellationToken.None);

        return output.ToArray();
    }

    // Результат читается обратно: FFmpeg декодирует его без ошибок.
    private static async Task AssertDecodes(byte[] data)
    {
        using MemoryStream input = new(data);
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg().Path!,
            ["-hide_banner", "-loglevel", "error", "-i", "pipe:0", "-f", "null", "-"],
            StandardInput: input));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.ErrorOutput);
    }

    public static IEnumerable<object[]> Pairs()
    {
        string[] samples = ["sample-id3.mp3", "sample.wav", "sample-vorbis.ogg", "sample-opus.ogg", "sample.flac", "sample.m4a"];
        string[] targets = ["MP3", "WAV", "OGG", "FLAC", "M4A"];
        foreach (string sample in samples)
        {
            foreach (string target in targets)
            {
                yield return new object[] { sample, target };
            }
        }
    }

    [ExternalTheory]
    [MemberData(nameof(Pairs))]
    public async Task EveryAudioPair_ProducesAFileOfTheTargetType_ThatDecodesBack(string sample, string target)
    {
        FileFormat to = target switch
        {
            "MP3" => FileFormat.Mp3,
            "WAV" => FileFormat.Wav,
            "FLAC" => FileFormat.Flac,
            "M4A" => FileFormat.M4a,
            _ => FileFormat.Ogg,
        };

        byte[] result = await Convert(Sample(sample), to);

        Assert.Same(to, SignatureMatcher.Match(result));
        await AssertDecodes(result);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task SampleRateAndChannels_AreAppliedToWav()
    {
        byte[] result = await Convert(Sample("sample.wav"), FileFormat.Wav, new AudioOptions { SampleRate = 8000, Channels = 2 });

        // Заголовок WAV: число каналов — байты 22–23, частота — байты 24–27.
        Assert.Equal(2, BitConverter.ToUInt16(result, 22));
        Assert.Equal(8000, BitConverter.ToInt32(result, 24));
    }

    [ExternalFact]
    public async Task WavHeaderSizes_AreCorrect_BecauseItWasWrittenToAFileNotAPipe()
    {
        byte[] result = await Convert(Sample("sample-vorbis.ogg"), FileFormat.Wav);

        uint riffSize = BitConverter.ToUInt32(result, 4);
        Assert.Equal((uint)(result.Length - 8), riffSize);
    }

    [ExternalFact]
    public async Task Mp3_HasTheDurationHeader_BecauseItWasWrittenToAFileNotAPipe()
    {
        // Служебный заголовок MP3 (Xing — переменный битрейт, Info — постоянный) пишется после кодирования, возвращаясь в начало выхода;
        // в канал он не попадает, и тогда длительность файла определяется по первым кадрам, то есть неверно.
        byte[] result = await Convert(Sample("sample-opus.ogg"), FileFormat.Mp3);

        string head = System.Text.Encoding.ASCII.GetString(result, 0, Math.Min(result.Length, 1000));
        Assert.True(head.Contains("Xing") || head.Contains("Info"), "В начале MP3 нет заголовка Xing/Info.");
    }

    [ExternalFact]
    public async Task Flac_IsLossless_WavThroughFlacComesBackWithTheSameAudioData()
    {
        byte[] wav = Sample("sample.wav");

        byte[] flac = await Convert(wav, FileFormat.Flac);
        byte[] back = await Convert(flac, FileFormat.Wav);

        // Заголовки WAV могут отличаться служебными полями; данные звука (после заголовка из 44 байт) должны совпасть побайтно.
        Assert.Same(FileFormat.Flac, SignatureMatcher.Match(flac));
        Assert.Equal(wav.AsSpan(44).ToArray(), back.AsSpan(44).ToArray());
    }

    [ExternalFact]
    public async Task M4a_IsAnMp4WithTheAudioBrand_AndReadsBack()
    {
        byte[] m4a = await Convert(Sample("sample.wav"), FileFormat.M4a);

        Assert.Same(FileFormat.M4a, SignatureMatcher.Match(m4a));
        Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(m4a, 4, 4));
        await AssertDecodes(m4a);
    }

    [ExternalFact]
    public async Task M4a_DefaultBitrateIsReplacedByTheSetting()
    {
        byte[] wav = Sample("sample.wav");

        byte[] low = await Convert(wav, FileFormat.M4a, new AudioOptions { Bitrate = 24_000 });
        byte[] high = await Convert(wav, FileFormat.M4a, new AudioOptions { Bitrate = 256_000 });

        Assert.True(low.Length < high.Length, $"24 кбит/с дал {low.Length} байт, 256 кбит/с — {high.Length}");
    }

    [ExternalFact]
    public async Task Bitrate_ChangesTheSize_LowerBitrateIsSmaller()
    {
        byte[] wav = Sample("sample.wav");

        byte[] low = await Convert(wav, FileFormat.Mp3, new AudioOptions { Bitrate = 32_000 });
        byte[] high = await Convert(wav, FileFormat.Mp3, new AudioOptions { Bitrate = 192_000 });

        Assert.True(low.Length < high.Length, $"32 кбит/с дал {low.Length} байт, 192 кбит/с — {high.Length}");
    }

    [ExternalFact]
    public async Task GarbageInput_IsToolFailed_WithExitCodeAndLastLines()
    {
        byte[] garbage = new byte[3000];
        new Random(7).NextBytes(garbage);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () => await Convert(garbage, FileFormat.Mp3));

        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.NotEqual(0, ex.ToolExitCode);
        Assert.Contains("Invalid data", ex.ToolOutput);
    }

    [ExternalFact]
    public async Task EmptyInput_IsToolFailed()
    {
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () => await Convert([], FileFormat.Wav));

        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task Cancelled_BeforeStart_ThrowsOperationCanceled_AndLeavesNoTempFiles()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        using MemoryStream source = new(Sample("sample.wav"));
        using MemoryStream output = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _engine.ConvertAsync(new EngineRequest(source, output, FileFormat.Wav, null, Ffmpeg(), _temp), cts.Token));

        Assert.Empty(Directory.GetFiles(_temp));
    }

    [Fact]
    public void Pairs_AreTwentyFive_AndCanConvertMatchesThem()
    {
        Assert.Equal(25, _engine.Pairs.Count);
        Assert.All(_engine.Pairs, p => Assert.True(_engine.CanConvert(p.From, p.To)));
        Assert.False(_engine.CanConvert(FileFormat.Mp3, FileFormat.Jpeg));
        Assert.False(_engine.CanConvert(FileFormat.Unknown, FileFormat.Mp3));
        Assert.Equal("ffmpeg", _engine.ToolName);
    }
}
