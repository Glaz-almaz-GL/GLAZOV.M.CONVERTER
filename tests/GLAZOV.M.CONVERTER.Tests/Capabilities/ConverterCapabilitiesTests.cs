using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Capabilities;

/// <summary>
/// Каталог возможностей: пары движков, готовность по наличию программы, стабильный порядок, подсказка по программе.
/// Программа и движок подставные; проверки на встроенных движках — в конце.
/// </summary>
public sealed class ConverterCapabilitiesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public ConverterCapabilitiesTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private string ToolFile(string name)
    {
        string path = Path.Combine(_folder, OperatingSystem.IsWindows() ? name + ".exe" : name);
        File.WriteAllBytes(path, [0]);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        return path;
    }

    private static ConverterCapabilities Capabilities(IEnumerable<IConversionEngine> engines, ConverterSettings? settings = null)
    {
        ConverterSettingsSnapshot snapshot = ConverterSettingsSnapshot.Create(settings ?? new ConverterSettings());
        ToolLocator locator = new(snapshot, new ToolEnvironment(null, null));
        return new ConverterCapabilities(new EngineRegistry(engines), locator);
    }

    [Fact]
    public void All_MarksReadyOnlyWhereTheToolIsFound_AndKeepsAStableOrder()
    {
        string present = ToolFile("есть");
        ConverterSettings settings = new() { ToolPaths = { ["есть"] = present } };
        ConverterCapabilities caps = Capabilities([new Engine("есть", (TestFormats.Alpha, TestFormats.Beta)), new Engine("нет", (TestFormats.Beta, TestFormats.Gamma))], settings);

        IReadOnlyList<Conversion> first = caps.All;
        IReadOnlyList<Conversion> second = caps.All;

        Assert.Equal(2, first.Count);
        Assert.Equal((TestFormats.Alpha, TestFormats.Beta, ConversionAvailability.Ready), (first[0].From, first[0].To, first[0].Availability));
        Assert.Equal((TestFormats.Beta, TestFormats.Gamma, ConversionAvailability.ToolMissing), (first[1].From, first[1].To, first[1].Availability));
        Assert.Equal(first.Select(c => (c.From, c.To, c.Availability)), second.Select(c => (c.From, c.To, c.Availability)));
    }

    [Fact]
    public void CanConvert_IsAboutSupport_NotAboutTheToolBeingInstalled()
    {
        ConverterCapabilities caps = Capabilities([new Engine("нет", (TestFormats.Alpha, TestFormats.Beta))]);

        Assert.True(caps.CanConvert(TestFormats.Alpha, TestFormats.Beta));
        Assert.False(caps.CanConvert(TestFormats.Beta, TestFormats.Alpha));
        Assert.False(caps.CanConvert(FileFormat.Unknown, TestFormats.Beta));
        Assert.False(caps.CanConvert(TestFormats.Alpha, TestFormats.Delta));
    }

    [Fact]
    public void Tool_ReturnsStatusWithHint_WhenMissing_AndPath_WhenFound()
    {
        string present = ToolFile("есть");
        ConverterSettings settings = new() { ToolPaths = { ["есть"] = present } };
        ConverterCapabilities caps = Capabilities([new Engine("есть", (TestFormats.Alpha, TestFormats.Beta)), new Engine("нет", (TestFormats.Beta, TestFormats.Gamma))], settings);

        ToolStatus found = caps.Tool("ЕСТЬ");
        ToolStatus missing = caps.Tool("нет");

        Assert.True(found.Found);
        Assert.Equal(present, found.Path);
        Assert.False(missing.Found);
        Assert.Contains("«нет»", missing.InstallHint);
    }

    [Fact]
    public void Tool_UnknownName_IsAUsageErrorListingTheKnownOnes()
    {
        ConverterCapabilities caps = Capabilities([new Engine("программа", (TestFormats.Alpha, TestFormats.Beta))]);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => caps.Tool("другая"));

        Assert.Contains("другая", ex.Message);
        Assert.Contains("программа", ex.Message);
        Assert.Throws<ArgumentException>(() => caps.Tool(" "));
    }

    [Fact]
    public void NullFormats_AreUsageErrors()
    {
        ConverterCapabilities caps = Capabilities([new Engine("x", (TestFormats.Alpha, TestFormats.Beta))]);

        Assert.Throws<ArgumentNullException>(() => caps.CanConvert(null!, TestFormats.Beta));
        Assert.Throws<ArgumentNullException>(() => caps.CanConvert(TestFormats.Alpha, null!));
    }

    [Fact]
    public void RealConverter_ListsAllPairs_AndKnowsFfmpeg()
    {
        ConverterCapabilities caps = new Converter().Capabilities;

        IReadOnlyList<Conversion> all = caps.All;

        Assert.Equal(143, all.Count);
        Assert.True(caps.CanConvert(FileFormat.Ogg, FileFormat.Mp3));
        Assert.False(caps.CanConvert(FileFormat.Ogg, FileFormat.Jpeg));
        Assert.True(caps.CanConvert(FileFormat.Png, FileFormat.Jpeg));
        Assert.Equal("ffmpeg", caps.Tool("ffmpeg").Name);
    }

    [ExternalFact]
    public void RealConverter_WithFfmpegInstalled_AllPairsAreReady()
    {
        ConverterCapabilities caps = new Converter().Capabilities;

        Assert.All(caps.All.Where(c => !c.To.Equals(FileFormat.Pdf)), c => Assert.Equal(ConversionAvailability.Ready, c.Availability));
        Assert.True(caps.Tool("ffmpeg").Found);
    }

    private sealed class Engine(string toolName, params (FileFormat From, FileFormat To)[] pairs) : IConversionEngine
    {
        public IReadOnlyList<ToolSpec> Tools { get; } = [new ToolSpec(toolName)];

        public bool SupportsOperations => false;

        public IReadOnlyList<ConversionPair> Pairs { get; } = [.. pairs.Select(p => new ConversionPair(p.From, p.To))];

        public Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
