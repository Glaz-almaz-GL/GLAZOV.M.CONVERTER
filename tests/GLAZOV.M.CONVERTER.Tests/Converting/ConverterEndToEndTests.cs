using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Converting;

/// <summary>
/// Сквозные проверки публичного вызова на подставных форматах и подставном движке: файл, поток, память.
/// Движок пишет заголовок целевого формата, поэтому результат узнаёт сама библиотека.
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

    // The engine converts Alpha to Beta: the Beta header followed by the input.
    private static LambdaEngine NewEngine(Action<ConversionRequest>? inspect = null)
    {
        return new LambdaEngine(null, [new ConversionPair(TestFormats.Alpha, TestFormats.Beta)], marker: TestFormats.Header(TestFormats.Beta)) { Inspect = inspect };
    }

    private static Converter NewConverter(LambdaEngine engine)
    {
        ConverterSettings settings = new();
        settings.Extensions.Add(TestExtensions.Create(engine));
        return new Converter(settings);
    }

    [Fact]
    public async Task AlphaFile_ToBetaFile_IsABeta_ThatTheLibraryItselfRecognizes()
    {
        string input = Path.Combine(_folder, "source.alpha");
        await File.WriteAllBytesAsync(input, TestFormats.Sample(TestFormats.Alpha));
        string output = Path.Combine(_folder, "result.beta");
        Converter converter = NewConverter(NewEngine());

        ConversionResult result = await converter.ConvertAsync(Source.File(input), Target.File(output, TestFormats.Beta));

        Assert.True(result.Succeeded);
        Assert.Same(TestFormats.Alpha, result.SourceType);
        Assert.Same(TestFormats.Beta, await converter.DetectAsync(Source.File(output)));
    }

    [Fact]
    public async Task AlphaStream_ToBetaStream_WorksFromAndToStreams()
    {
        Converter converter = NewConverter(NewEngine());
        using MemoryStream input = new(TestFormats.Sample(TestFormats.Alpha));
        using MemoryStream output = new();

        await converter.ConvertAsync(Source.Stream(input), Target.Stream(output, TestFormats.Beta));

        output.Position = 0;
        Assert.Same(TestFormats.Beta, await converter.DetectAsync(Source.Stream(output)));
    }

    [Fact]
    public async Task AlphaFile_ToMemory_GivesBytesOfTheTargetType()
    {
        string input = Path.Combine(_folder, "source.alpha");
        await File.WriteAllBytesAsync(input, TestFormats.Sample(TestFormats.Alpha));
        Converter converter = NewConverter(NewEngine());

        ConversionResult result = await converter.ConvertAsync(Source.File(input), Target.Memory(TestFormats.Beta));

        Assert.NotNull(result.Bytes);
        using MemoryStream bytes = new(result.Bytes);
        Assert.Same(TestFormats.Beta, await converter.DetectAsync(Source.Stream(bytes)));
    }

    [Fact]
    public async Task OptionsWithParameters_ReachTheEngine()
    {
        AudioOptions? seen = null;
        Converter converter = NewConverter(NewEngine(request => seen = request.Options.Audio));
        using MemoryStream input = new(TestFormats.Sample(TestFormats.Alpha));

        await converter.ConvertAsync(
            Source.Stream(input),
            Target.Memory(TestFormats.Beta),
            new ConversionOptions { Audio = new AudioOptions { SampleRate = 8000, Channels = 1 } });

        Assert.NotNull(seen);
        Assert.Equal(8000, seen.SampleRate);
        Assert.Equal(1, seen.Channels);
    }

    [Fact]
    public async Task NotAFormatOfTheEngine_IsRefusedBeforeTheEngineIsEvenStarted()
    {
        string text = Path.Combine(_folder, "текст.alpha");
        await File.WriteAllTextAsync(text, "Это не ALPHA.");
        string output = Path.Combine(_folder, "never.beta");
        LambdaEngine engine = NewEngine();

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await NewConverter(engine).ConvertAsync(Source.File(text), Target.File(output, TestFormats.Beta)));

        // Plain text is a type of its own, with no way to Beta.
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Equal(0, engine.Calls);
        Assert.False(File.Exists(output));
    }
}
