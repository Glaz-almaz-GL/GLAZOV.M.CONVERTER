using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Extensions;

/// <summary>
/// The programs of an engine from any package: required and optional descriptions, what the engine gets, what it may report back.
/// </summary>
public sealed class EngineToolsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public EngineToolsTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private string MakeProgram(string name)
    {
        string path = Path.Combine(_folder, OperatingSystem.IsWindows() ? name + ".exe" : name);
        File.WriteAllBytes(path, [0]);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        return path;
    }

    private string Input()
    {
        string path = Path.Combine(_folder, "вход.dat");
        File.WriteAllBytes(path, "DEMO данные"u8.ToArray());
        return path;
    }

    private static Converter With(ConverterSettings settings, LambdaEngine engine)
    {
        settings.Extensions.Add(new ConfigurableExtension("с программами", [new PatternDetector("DEMO"u8.ToArray(), DemoExtension.Demo)], [engine]));
        return new Converter(settings);
    }

    [Fact]
    public async Task EngineGetsEveryDescribedProgram_TheFirstRequiredAsTool_AndAnOptionalMissingOneIsNotAnError()
    {
        string main = MakeProgram("главная");
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)])
        {
            Specs = [new ToolSpec("Главная"), new ToolSpec("помощница") { Required = false }],
        };
        ConverterSettings settings = new() { ToolPaths = { ["главная"] = main } };

        await With(settings, engine).ConvertAsync(Source.File(Input()), Target.Memory(FileFormat.Wav));

        ConversionRequest request = engine.Last!;
        Assert.Equal(main, request.Tool!.Path);
        Assert.Equal(2, request.Tools.Count);
        Assert.True(request.Tools["ГЛАВНАЯ"].Found);
        Assert.False(request.Tools["помощница"].Found);
    }

    [Fact]
    public async Task AnOptionalProgramThatIsFound_IsGivenToTheEngine()
    {
        string main = MakeProgram("главная");
        string helper = MakeProgram("помощница");
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)])
        {
            Specs = [new ToolSpec("главная"), new ToolSpec("помощница") { Required = false }],
        };
        ConverterSettings settings = new() { ToolPaths = { ["главная"] = main, ["помощница"] = helper } };

        await With(settings, engine).ConvertAsync(Source.File(Input()), Target.Memory(FileFormat.Wav));

        Assert.Equal(helper, engine.Last!.Tools["помощница"].Path);
    }

    [Fact]
    public async Task ASecondRequiredProgramThatIsMissing_IsToolNotFound_WithItsOwnHint_BeforeTheEngineRuns()
    {
        string main = MakeProgram("главная");
        ToolSpec second = new("вторая");
        foreach (ToolSystem system in Enum.GetValues<ToolSystem>())
        {
            second.InstallHints[system] = "Поставьте вторую.";
        }

        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]) { Specs = [new ToolSpec("главная"), second] };
        ConverterSettings settings = new() { ToolPaths = { ["главная"] = main } };

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await With(settings, engine).ConvertAsync(Source.File(Input()), Target.Memory(FileFormat.Wav)));

        Assert.Equal(ConversionFailureReason.ToolNotFound, ex.Reason);
        Assert.Equal("вторая", ex.ToolName);
        Assert.Contains("Поставьте вторую.", ex.Message);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task EngineFromAnotherPackage_CanReportThatItCopiedInsteadOfEncoding()
    {
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]) { Inspect = request => request.Reencoded = false };

        ConversionResult result = await With(new ConverterSettings(), engine).ConvertAsync(Source.File(Input()), Target.Memory(FileFormat.Wav));

        Assert.False(result.Reencoded);
    }

    [Fact]
    public async Task EngineThatDoesNotReport_LeavesReencodedTrue()
    {
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)]);

        ConversionResult result = await With(new ConverterSettings(), engine).ConvertAsync(Source.File(Input()), Target.Memory(FileFormat.Wav));

        Assert.True(result.Reencoded);
    }

    [Fact]
    public void CatalogAvailability_ReadsRequiredProgramsOnly_AndToolAnswersByDescription()
    {
        string main = MakeProgram("главная");
        LambdaEngine engine = new(null, [new ConversionPair(DemoExtension.Demo, FileFormat.Wav)])
        {
            Specs = [new ToolSpec("главная"), new ToolSpec("помощница") { Required = false }],
        };
        ConverterSettings settings = new() { ToolPaths = { ["главная"] = main } };

        ConverterCapabilities caps = With(settings, engine).Capabilities;

        Assert.Equal(ConversionAvailability.Ready, Assert.Single(caps.All, c => c.From.Equals(DemoExtension.Demo)).Availability);
        Assert.Equal(main, caps.Tool("главная").Path);
        Assert.Throws<ArgumentException>(() => caps.Tool("помощница"));
    }
}
