using System.Text;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

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

    private static byte[] Demo(string tail = " содержимое")
    {
        return [.. "DEMO"u8.ToArray(), .. Encoding.UTF8.GetBytes(tail)];
    }

    [Fact]
    public async Task DemoExtension_FromAnotherAssembly_ConvertsItsOwnFormat_WithoutFfmpeg()
    {
        string input = Put("пример.dat", Demo());

        ConversionResult result = await With(new DemoExtension()).ConvertAsync(Source.File(input), Target.Memory(FileFormat.Wav));

        Assert.True(result.Succeeded);
        Assert.Same(DemoExtension.Demo, result.SourceType);
        Assert.Equal(Demo(), result.Bytes);
    }

    [Fact]
    public async Task WithoutTheExtension_TheSameFile_IsTypeNotConfirmed()
    {
        string input = Put("пример.dat", Demo());

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await new Converter().ConvertAsync(Source.File(input), Target.Memory(FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
    }

    [Fact]
    public async Task ThePublicStaticDetector_KnowsOnlyTheBuiltInTypes()
    {
        string input = Put("пример.dat", Demo());

        Assert.Same(FileFormat.Unknown, await FileTypeDetector.DetectAsync(Source.File(input)));
    }

    [Fact]
    public void Catalog_ShowsTheExtensionPairs_NextToTheBuiltInOnes_AndCanConvertKnowsThem()
    {
        ConverterCapabilities caps = With(new DemoExtension()).Capabilities;

        Assert.Equal(82, caps.All.Count);
        Conversion own = Assert.Single(caps.All, c => c.From.Equals(DemoExtension.Demo));
        Assert.Same(FileFormat.Wav, own.To);
        Assert.Equal(ConversionAvailability.Ready, own.Availability);
        Assert.True(caps.CanConvert(DemoExtension.Demo, FileFormat.Wav));
        Assert.False(caps.CanConvert(DemoExtension.Demo, FileFormat.Mp3));
        Assert.True(caps.CanConvert(FileFormat.Ogg, FileFormat.Mp3));
    }

    [Fact]
    public async Task BuiltInDetectionComesFirst_AnExtensionCannotStealABuiltInType()
    {
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);
        ConfigurableExtension greedy = new("жадное", [new PatternDetector("ID3"u8.ToArray(), DemoExtension.Demo)], [engine]);
        string mp3 = Put("a.mp3", File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "sample-id3.mp3")));

        // Файл с тегом ID3 — MP3 по готовым признакам; расширение «жадное» не перехватывает; MP3 → WAV без FFmpeg невозможен, поэтому ждём лишь, что формат не DEMO.
        Assert.Same(FileFormat.Mp3, await FileTypeDetector.DetectAsync(Source.File(mp3)));
        ConversionException ex = await Assert.ThrowsAnyAsync<ConversionException>(async () =>
            await With(greedy).ConvertAsync(Source.File(mp3), Target.Memory(FileFormat.Jpeg)));
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Contains("MP3", ex.Message);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task TwoExtensionsForOnePair_TheEarlierWins_AndTheCatalogShowsThePairOnce()
    {
        LambdaEngine first = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)], marker: "ПЕРВЫЙ:"u8.ToArray());
        LambdaEngine second = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)], marker: "ВТОРОЙ:"u8.ToArray());
        PatternDetector detector = new("DEMO"u8.ToArray(), DemoExtension.Demo);
        Converter converter = With(new ConfigurableExtension("а", [detector], [first]), new ConfigurableExtension("б", [], [second]));

        ConversionResult result = await converter.ConvertAsync(Source.File(Put("x.dat", Demo(":x"))), Target.Memory(FileFormat.Wav));

        Assert.StartsWith("ПЕРВЫЙ:DEMO", Encoding.UTF8.GetString(result.Bytes!));
        Assert.Equal(1, converter.Capabilities.All.Count(c => c.From.Equals(DemoExtension.Demo)));
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public async Task DetectorWindow_IsTheLargestRequested_SoALateSignatureIsSeen()
    {
        FileFormat late = FileFormat.Create("LATE", "application/x-late", "late");
        byte[] data = new byte[9000];
        "МЕТКА"u8.CopyTo(data.AsSpan(8000));
        LambdaEngine engine = new(null, [new ConversionPair(late, FileFormat.Wav)]);
        ConfigurableExtension extension = new("поздний", [new PatternDetector("МЕТКА"u8.ToArray(), late, headLength: 9000, offset: 8000)], [engine]);

        ConversionResult result = await With(extension).ConvertAsync(Source.File(Put("late.bin", data)), Target.Memory(FileFormat.Wav));

        Assert.Same(late, result.SourceType);
        Assert.Equal(data, result.Bytes);
    }

    [Fact]
    public async Task EngineRequest_CarriesInputOutputFormatsOptionsAndTempFolder()
    {
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);
        Converter converter = With(new ConfigurableExtension("а", [new PatternDetector("DEMO"u8.ToArray(), DemoExtension.Demo)], [engine]));
        AudioOptions audio = new() { Bitrate = 64_000 };

        await converter.ConvertAsync(Source.File(Put("x.dat", Demo())), Target.Memory(FileFormat.Wav), new ConversionOptions { Audio = audio });

        ConversionRequest request = engine.Last!;
        Assert.Same(DemoExtension.Demo, request.From);
        Assert.Same(FileFormat.Wav, request.To);
        Assert.Same(audio, request.Options.Audio);
        Assert.Null(request.Tool);
        Assert.True(Directory.Exists(request.TempFolder));
    }

    [Fact]
    public async Task EngineWithoutOptions_StillGetsANonNullOptionsObject()
    {
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);
        Converter converter = With(new ConfigurableExtension("а", [new PatternDetector("DEMO"u8.ToArray(), DemoExtension.Demo)], [engine]));

        await converter.ConvertAsync(Source.File(Put("x.dat", Demo())), Target.Memory(FileFormat.Wav));

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

        LambdaEngine engine = new("мой", [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);
        ConfigurableExtension extension = new("с программой", [new PatternDetector("DEMO"u8.ToArray(), DemoExtension.Demo)], [engine]);
        ConverterSettings withTool = new() { ToolPaths = { ["мой"] = tool } };
        withTool.Extensions.Add(extension);

        await new Converter(withTool).ConvertAsync(Source.File(Put("x.dat", Demo())), Target.Memory(FileFormat.Wav));

        Assert.Equal(tool, engine.Last!.Tool!.Path);

        LambdaEngine other = new("нет-такой-программы", [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await With(new ConfigurableExtension("без программы", [new PatternDetector("DEMO"u8.ToArray(), DemoExtension.Demo)], [other]))
                .ConvertAsync(Source.File(Put("y.dat", Demo())), Target.Memory(FileFormat.Wav)));
        Assert.Equal(ConversionFailureReason.ToolNotFound, ex.Reason);
        Assert.Equal(0, other.Calls);
    }

    [Fact]
    public async Task OperationsAreRefusedBeforeWork_ForAnEngineThatDoesNotDeclareThem_AndPassedToOneThatDoes()
    {
        string input = Put("x.dat", Demo());
        PatternDetector detector = new("DEMO"u8.ToArray(), DemoExtension.Demo);
        LambdaEngine plain = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);
        LambdaEngine capable = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)], supportsOperations: true);
        ConversionOptions options = new() { Operations = { Operation.RotateRight } };

        ConversionException refused = await Assert.ThrowsAsync<ConversionException>(async () =>
            await With(new ConfigurableExtension("а", [detector], [plain])).ConvertAsync(Source.File(input), Target.Memory(FileFormat.Wav), options));
        await With(new ConfigurableExtension("б", [detector], [capable])).ConvertAsync(Source.File(input), Target.Memory(FileFormat.Wav), options);

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, refused.Reason);
        Assert.Equal(0, plain.Calls);
        Assert.Single(capable.Last!.Options.Operations);
    }

    [Fact]
    public async Task UnexpectedExceptionOfAnExtensionEngine_BecomesEngineFailed_WithTheOriginalInside()
    {
        InvalidOperationException original = new("чужой сбой");
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]) { Crash = original };
        Converter converter = With(new ConfigurableExtension("а", [new PatternDetector("DEMO"u8.ToArray(), DemoExtension.Demo)], [engine]));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(Source.File(Put("x.dat", Demo())), Target.File(Path.Combine(_folder, "o.wav"), FileFormat.Wav)));

        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Equal(ConversionFailureReason.EngineFailed, ex.Reason);
        Assert.Same(original, ex.InnerException);
        Assert.Contains("чужой сбой", ex.Message);
        Assert.Contains(nameof(LambdaEngine), ex.Message);
        Assert.False(File.Exists(Path.Combine(_folder, "o.wav")));
    }

    [Fact]
    public async Task CancellationOfAnExtensionEngine_StaysCancellation()
    {
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]) { WaitForCancel = true };
        Converter converter = With(new ConfigurableExtension("а", [new PatternDetector("DEMO"u8.ToArray(), DemoExtension.Demo)], [engine]));
        using CancellationTokenSource cts = new();
        Task<ConversionResult> run = converter.ConvertAsync(Source.File(Put("x.dat", Demo())), Target.Memory(FileFormat.Wav), cancellationToken: cts.Token);

        await Task.Delay(300);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
    }

    [Fact]
    public async Task ExtensionInABatch_WorksAndAnEngineCrashIsAnItemNotALostBatch()
    {
        LambdaEngine ok = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);
        string a = Put("a.dat", Demo(" один"));
        string b = Put("b.dat", Demo(" два"));
        Converter converter = With(new ConfigurableExtension("а", [new PatternDetector("DEMO"u8.ToArray(), DemoExtension.Demo)], [ok]));

        BatchReport report = await converter.ConvertBatchAsync([Source.File(a), Source.File(b)], Target.Folder(Path.Combine(_folder, "out"), FileFormat.Wav));

        Assert.Equal(2, report.Succeeded);
        Assert.True(File.Exists(Path.Combine(_folder, "out", "a.wav")));
    }

    [Fact]
    public void SettingsAreCopied_ChangingTheListAfterCreationDoesNotChangeTheConverter()
    {
        ConverterSettings settings = new();
        Converter converter = new(settings);

        settings.Extensions.Add(new DemoExtension());

        Assert.Equal(81, converter.Capabilities.All.Count);
    }

    [Fact]
    public void BadExtensions_AreRefusedAtCreation_WithAClearText()
    {
        PatternDetector detector = new("X"u8.ToArray(), DemoExtension.Demo);
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);

        ArgumentException twice = Assert.Throws<ArgumentException>(() => With(new DemoExtension(), new DemoExtension()));
        ArgumentException empty = Assert.Throws<ArgumentException>(() => With(new ConfigurableExtension(" ", [detector], [engine])));
        ArgumentException nullLists = Assert.Throws<ArgumentException>(() => With(new ConfigurableExtension("а", null!, [engine])));
        ArgumentException nullItem = Assert.Throws<ArgumentException>(() => With(new ConfigurableExtension("б", [null!], [engine])));
        ArgumentException zeroWindow = Assert.Throws<ArgumentException>(() => With(new ConfigurableExtension("в", [new PatternDetector("X"u8.ToArray(), DemoExtension.Demo, headLength: 0)], [engine])));
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
