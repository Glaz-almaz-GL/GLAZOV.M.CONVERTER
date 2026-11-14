using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Formats.Options;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;
using System.Text;

namespace GLAZOV.M.CONVERTER.Tests.Extensions;

/// <summary>
/// Публичная точка расширения (эталон v0.5, D-012): расширение, написанное в ДРУГОЙ сборке только на публичных типах,
/// подключается настройкой, определяет свой формат, конвертирует, видно в каталоге; порядок, отказы, чужие сбои.
/// </summary>
public sealed class ExtensionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public ExtensionTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static Converter With(params IConverterExtension[] extensions)
    {
        ConverterSettings settings = new();
        foreach (IConverterExtension extension in extensions)
        {
            settings.Extensions.Add(extension);
        }

        return new Converter(settings);
    }

    private string Put(string name, byte[] data)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static readonly FileFormat Alpha = TestFormats.Alpha;

    private static readonly FileFormat Beta = TestFormats.Beta;

    private static byte[] AlphaFile()
    {
        return TestFormats.Sample(Alpha);
    }

    private static byte[] AlphaText()
    {
        return [.. TestFormats.Header(Alpha), .. Encoding.UTF8.GetBytes(" содержимое")];
    }

    private static LambdaEngine AlphaToBeta(byte[]? marker = null, bool supportsOperations = false, string? toolName = null)
    {
        return new LambdaEngine(toolName, [new ConversionPair(Alpha, Beta)], supportsOperations, marker);
    }

    private static int BuiltInPairs()
    {
        return new Converter().Capabilities.All.Count;
    }

    [Fact]
    public async Task Extension_FromAnotherAssembly_ConvertsItsOwnFormat_WithoutAnExternalProgram()
    {
        byte[] data = AlphaFile();
        string input = Put("пример.dat", data);

        ConversionResult result = await With(TestExtensions.Create(AlphaToBeta())).ConvertAsync(Source.File(input), Target.Memory(Beta));

        Assert.True(result.Succeeded);
        Assert.Same(Alpha, result.SourceType);
        Assert.Equal(data, result.Bytes);
    }

    [Fact]
    public async Task WithoutTheExtension_TheSameFile_IsNotRecognised()
    {
        string input = Put("пример.dat", AlphaText());

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await new Converter().ConvertAsync(Source.File(input), Target.Memory(Beta)));

        // No detector knows this content: only the extension names it its format.
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
    }

    [Fact]
    public async Task WithoutExtensions_NoTypeIsKnown()
    {
        string input = Put("пример.dat", AlphaText());

        FileFormat detected = await new Converter().DetectAsync(Source.File(input));

        Assert.Same(FileFormat.Unknown, detected);
    }

    [Fact]
    public void Catalog_ShowsTheExtensionPairs_NextToTheBuiltInOnes_AndCanConvertKnowsThem()
    {
        ConverterCapabilities caps = With(TestExtensions.Create(AlphaToBeta())).Capabilities;

        Assert.Equal(BuiltInPairs() + 1, caps.All.Count);
        Conversion own = Assert.Single(caps.All, c => c.From.Equals(Alpha));
        Assert.Same(Beta, own.To);
        Assert.Equal(ConversionAvailability.Ready, own.Availability);
        Assert.True(caps.CanConvert(Alpha, Beta));
        Assert.False(caps.CanConvert(Alpha, TestFormats.Gamma));
    }

    [Fact]
    public async Task TheEarlierExtensionDetectsFirst_ALaterOneCannotStealItsType()
    {
        LambdaEngine engine = AlphaToBeta();
        ConfigurableExtension greedy = new("жадное", [new PatternDetector("ID3"u8.ToArray(), Alpha)], [engine]);
        string mp3 = Put("a.mp3", await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Samples", "sample-id3.mp3")));

        // The Formats extension is connected first: a file with the ID3 tag is MP3; the later extension "greedy" does not take it; no engine knows MP3 → JPEG.
        Assert.Same(FileFormat.Mp3, await With(new FormatsExtension(), greedy).DetectAsync(Source.File(mp3)));
        ConversionException ex = await Assert.ThrowsAnyAsync<ConversionException>(async () =>
            await With(new FormatsExtension(), greedy).ConvertAsync(Source.File(mp3), Target.Memory(FileFormat.Jpeg)));
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Contains("MP3", ex.Message);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task TwoExtensionsForOnePair_TheEarlierWins_AndTheCatalogShowsThePairOnce()
    {
        LambdaEngine first = AlphaToBeta("ПЕРВЫЙ:"u8.ToArray());
        LambdaEngine second = AlphaToBeta("ВТОРОЙ:"u8.ToArray());
        Converter converter = With(TestExtensions.Create(first), new ConfigurableExtension("б", [], [second]));

        ConversionResult result = await converter.ConvertAsync(Source.File(Put("x.dat", AlphaFile())), Target.Memory(Beta));

        Assert.StartsWith("ПЕРВЫЙ:~TST-ALPHA", Encoding.UTF8.GetString(result.Bytes!));
        Assert.Equal(1, converter.Capabilities.All.Count(c => c.From.Equals(Alpha)));
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public async Task DetectorWindow_IsTheLargestRequested_SoALateSignatureIsSeen()
    {
        FileFormat late = FileFormat.Create("LATE", "application/x-late", "late");
        byte[] data = new byte[9000];
        "МЕТКА"u8.CopyTo(data.AsSpan(8000));
        LambdaEngine engine = new(null, [new ConversionPair(late, Beta)]);
        ConfigurableExtension extension = new("поздний", [new PatternDetector("МЕТКА"u8.ToArray(), late, headLength: 9000, offset: 8000)], [engine]);

        ConversionResult result = await With(extension).ConvertAsync(Source.File(Put("late.bin", data)), Target.Memory(Beta));

        Assert.Same(late, result.SourceType);
        Assert.Equal(data, result.Bytes);
    }

    [Fact]
    public async Task EngineRequest_CarriesInputOutputFormatsOptionsAndTempFolder()
    {
        LambdaEngine engine = AlphaToBeta();
        Converter converter = With(TestExtensions.Create(engine));
        AudioOptions audio = new() { Bitrate = 64_000 };

        await converter.ConvertAsync(Source.File(Put("x.dat", AlphaFile())), Target.Memory(Beta), new ConversionOptions { Audio = audio });

        ConversionRequest request = engine.Last!;
        Assert.Same(Alpha, request.From);
        Assert.Same(Beta, request.To);
        Assert.Same(audio, request.Options.Audio);
        Assert.Null(request.Tool);
        Assert.True(Directory.Exists(request.TempFolder));
    }

    [Fact]
    public async Task EngineWithoutOptions_StillGetsANonNullOptionsObject()
    {
        LambdaEngine engine = AlphaToBeta();
        Converter converter = With(TestExtensions.Create(engine));

        await converter.ConvertAsync(Source.File(Put("x.dat", AlphaFile())), Target.Memory(Beta));

        Assert.NotNull(engine.Last!.Options);
        Assert.Empty(engine.Last.Options.Operations);
    }

    [Fact]
    public async Task EngineWithAToolName_GetsItFoundBySettings_AndMissingToolIsToolNotFound()
    {
        string tool = Put(OperatingSystem.IsWindows() ? "мой.exe" : "мой", [0]);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        LambdaEngine engine = AlphaToBeta(toolName: "мой");
        ConverterSettings withTool = new() { ToolPaths = { ["мой"] = tool } };
        withTool.Extensions.Add(TestExtensions.Create(engine));

        await new Converter(withTool).ConvertAsync(Source.File(Put("x.dat", AlphaFile())), Target.Memory(Beta));

        Assert.Equal(tool, engine.Last!.Tool!.Path);

        LambdaEngine other = AlphaToBeta(toolName: "нет-такой-программы");
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await With(TestExtensions.Create(other))
                .ConvertAsync(Source.File(Put("y.dat", AlphaFile())), Target.Memory(Beta)));
        Assert.Equal(ConversionFailureReason.ToolNotFound, ex.Reason);
        Assert.Equal(0, other.Calls);
    }

    [Fact]
    public async Task OperationsAreRefusedBeforeWork_ForAnEngineThatDoesNotDeclareThem_AndPassedToOneThatDoes()
    {
        string input = Put("x.dat", AlphaFile());
        LambdaEngine plain = AlphaToBeta();
        LambdaEngine capable = AlphaToBeta(supportsOperations: true);
        ConversionOptions options = new() { Operations = { VisualOperations.RotateRight } };

        ConversionException refused = await Assert.ThrowsAsync<ConversionException>(async () =>
            await With(TestExtensions.Create(plain)).ConvertAsync(Source.File(input), Target.Memory(Beta), options));
        await With(TestExtensions.Create(capable)).ConvertAsync(Source.File(input), Target.Memory(Beta), options);

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, refused.Reason);
        Assert.Equal(0, plain.Calls);
        Assert.Single(capable.Last!.Options.Operations);
    }

    [Fact]
    public async Task UnexpectedExceptionOfAnExtensionEngine_BecomesEngineFailed_WithTheOriginalInside()
    {
        InvalidOperationException original = new("чужой сбой");
        LambdaEngine engine = new(null, [new ConversionPair(Alpha, Beta)]) { Crash = original };
        Converter converter = With(TestExtensions.Create(engine));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(Source.File(Put("x.dat", AlphaFile())), Target.File(Path.Combine(_folder, "o.beta"), Beta)));

        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Equal(ConversionFailureReason.EngineFailed, ex.Reason);
        Assert.Same(original, ex.InnerException);
        Assert.Contains("чужой сбой", ex.Message);
        Assert.Contains(nameof(LambdaEngine), ex.Message);
        Assert.False(File.Exists(Path.Combine(_folder, "o.beta")));
    }

    [Fact]
    public async Task CancellationOfAnExtensionEngine_StaysCancellation()
    {
        LambdaEngine engine = new(null, [new ConversionPair(Alpha, Beta)]) { WaitForCancel = true };
        Converter converter = With(TestExtensions.Create(engine));
        using CancellationTokenSource cts = new();
        Task<ConversionResult> run = converter.ConvertAsync(Source.File(Put("x.dat", AlphaFile())), Target.Memory(Beta), cancellationToken: cts.Token);

        await Task.Delay(300);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
    }

    [Fact]
    public async Task ExtensionInABatch_WorksAndAnEngineCrashIsAnItemNotALostBatch()
    {
        LambdaEngine ok = AlphaToBeta();
        string a = Put("a.dat", TestFormats.Sample(Alpha, 10));
        string b = Put("b.dat", TestFormats.Sample(Alpha, 20));
        Converter converter = With(TestExtensions.Create(ok));

        BatchReport report = await converter.ConvertBatchAsync([Source.File(a), Source.File(b)], Target.Folder(Path.Combine(_folder, "out"), Beta));

        Assert.Equal(2, report.Succeeded);
        Assert.True(File.Exists(Path.Combine(_folder, "out", "a.beta")));
    }

    [Fact]
    public void SettingsAreCopied_ChangingTheListAfterCreationDoesNotChangeTheConverter()
    {
        ConverterSettings settings = new();
        Converter converter = new(settings);

        settings.Extensions.Add(TestExtensions.Create(AlphaToBeta()));

        Assert.Equal(BuiltInPairs(), converter.Capabilities.All.Count);
    }

    [Fact]
    public void BadExtensions_AreRefusedAtCreation_WithAClearText()
    {
        PatternDetector detector = new("X"u8.ToArray(), Alpha);
        LambdaEngine engine = AlphaToBeta();

        ArgumentException twice = Assert.Throws<ArgumentException>(() => With(TestExtensions.Create(), TestExtensions.Create()));
        ArgumentException empty = Assert.Throws<ArgumentException>(() => With(new ConfigurableExtension(" ", [detector], [engine])));
        ArgumentException nullLists = Assert.Throws<ArgumentException>(() => With(new ConfigurableExtension("а", null!, [engine])));
        ArgumentException nullItem = Assert.Throws<ArgumentException>(() => With(new ConfigurableExtension("б", [null!], [engine])));
        ArgumentException zeroWindow = Assert.Throws<ArgumentException>(() => With(new ConfigurableExtension("в", [new PatternDetector("X"u8.ToArray(), Alpha, headLength: 0)], [engine])));
        ConverterSettings withNull = new();
        withNull.Extensions.Add(null!);
        ArgumentException nullEntry = Assert.Throws<ArgumentException>(() => new Converter(withNull));

        Assert.Contains("дважды", twice.Message);
        Assert.Contains("пустое имя", empty.Message);
        Assert.Contains("не заданы", nullLists.Message);
        Assert.Contains("пустые записи", nullItem.Message);
        Assert.Contains("HeadLength", zeroWindow.Message);
        Assert.Contains("пустая запись", nullEntry.Message);
    }
}
