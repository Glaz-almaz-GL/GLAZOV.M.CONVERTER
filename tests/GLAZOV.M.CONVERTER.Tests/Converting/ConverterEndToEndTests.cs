using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// Сквозные проверки публичного вызова на настоящем FFmpeg и настоящих файлах: файл, поток, память.
/// Нет FFmpeg — проверки пропускаются, а не проходят.
/// </summary>
public sealed class ConverterEndToEndTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public ConverterEndToEndTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static string SamplePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", name);
    }

    [ExternalFact]
    public async Task WavFile_ToMp3File_IsAnMp3_ThatTheLibraryItselfRecognizes()
    {
        string output = Path.Combine(_folder, "result.mp3");

        ConversionResult result = await new Converter().ConvertAsync(Source.File(SamplePath("sample.wav")), Target.File(output, FileFormat.Mp3));

        Assert.True(result.Succeeded);
        Assert.Same(FileFormat.Wav, result.SourceType);
        Assert.Same(FileFormat.Mp3, await FileTypeDetector.DetectAsync(Source.File(output)));
    }

    [ExternalFact]
    public async Task OggStream_ToWavStream_WorksFromAndToStreams()
    {
        using FileStream input = File.OpenRead(SamplePath("sample-vorbis.ogg"));
        using MemoryStream output = new();

        await new Converter().ConvertAsync(Source.Stream(input), Target.Stream(output, FileFormat.Wav));

        output.Position = 0;
        Assert.Same(FileFormat.Wav, await FileTypeDetector.DetectAsync(Source.Stream(output)));
    }

    [ExternalFact]
    public async Task Mp3File_ToMemory_GivesBytesOfTheTargetType()
    {
        ConversionResult result = await new Converter().ConvertAsync(Source.File(SamplePath("sample-id3.mp3")), Target.Memory(FileFormat.Ogg));

        Assert.NotNull(result.Bytes);
        using MemoryStream bytes = new(result.Bytes);
        Assert.Same(FileFormat.Ogg, await FileTypeDetector.DetectAsync(Source.Stream(bytes)));
    }

    [ExternalFact]
    public async Task WavWithParameters_AppliedToTheResult()
    {
        ConversionResult result = await new Converter().ConvertAsync(
            Source.File(SamplePath("sample.wav")),
            Target.Memory(FileFormat.Wav),
            new ConversionOptions { Audio = new AudioOptions { SampleRate = 8000, Channels = 1 } });

        Assert.Equal(1, BitConverter.ToUInt16(result.Bytes!, 22));
        Assert.Equal(8000, BitConverter.ToInt32(result.Bytes!, 24));
    }

    [ExternalFact]
    public async Task NotAudioFile_IsRefusedBeforeFfmpegIsEvenStarted()
    {
        string text = Path.Combine(_folder, "текст.mp3");
        await File.WriteAllTextAsync(text, "Это не звук.");
        string output = Path.Combine(_folder, "never.wav");

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await new Converter().ConvertAsync(Source.File(text), Target.File(output, FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
        Assert.False(File.Exists(output));
    }
}
