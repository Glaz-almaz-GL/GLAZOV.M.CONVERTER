using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Tests.Detection;

/// <summary>
/// Order of the detectors: labels of all extensions, then the second look at a container, then detectors by content.
/// </summary>
public sealed class DetectorOrderTests : IDisposable
{
    private static readonly FileFormat Label = FileFormat.Create("LBL", "application/x-lbl", "lbl");
    private static readonly FileFormat Guess = FileFormat.Create("GUESS", "application/x-guess", "guess");
    private static readonly FileFormat Inside = FileFormat.Create("INSIDE", "application/x-inside", "inside");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public DetectorOrderTests()
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

    private static ConfigurableExtension Ext(string name, params IFormatDetector[] detectors)
    {
        return new ConfigurableExtension(name, detectors, []) { Declared = [] };
    }

    private Source File(string text)
    {
        string path = Path.Combine(_folder, Guid.NewGuid().ToString("N"));
        System.IO.File.WriteAllBytes(path, Encoding.UTF8.GetBytes(text));
        return Source.File(path);
    }

    private static async Task<FileFormat> Detect(DetectorChain chain, byte[] bytes)
    {
        await using InputHandle input = new("test", new MemoryStream(bytes), ownsInner: true);
        return await InputDetection.DetectAsync(input, chain);
    }

    [Fact]
    public async Task ByContentDetector_DoesNotBeatALabelOfAnExtensionConnectedLater()
    {
        PatternDetector guesser = new([], Guess) { ByContent = true };
        PatternDetector labeled = new("LBL"u8.ToArray(), Label);
        Converter converter = With(Ext("угадывающее", guesser), Ext("с меткой", labeled));

        Assert.Equal(Label, await converter.DetectAsync(File("LBL и дальше")));
        Assert.Equal(Guess, await converter.DetectAsync(File("просто слова")));
    }

    [Fact]
    public async Task SecondLook_RunsOnlyWhenNoLabelNamedTheFormat_AndOnlyForALookingInput()
    {
        InspectingDetector inspector = new("INSP"u8.ToArray(), _ => Inside);
        Converter converter = With(Ext("второй взгляд", inspector), Ext("метка", new PatternDetector("INSP+"u8.ToArray(), Label)));

        Assert.Equal(Inside, await converter.DetectAsync(File("INSP обычный")));
        Assert.Equal(1, inspector.InspectCalls);

        Assert.Equal(Label, await converter.DetectAsync(File("INSP+ с меткой")));
        Assert.Equal(1, inspector.InspectCalls);

        Assert.Equal(FileFormat.Unknown, await converter.DetectAsync(File("другое начало")));
        Assert.Equal(1, inspector.InspectCalls);
    }

    [Fact]
    public async Task SecondLook_BeatsADetectorByContent()
    {
        InspectingDetector inspector = new("INSP"u8.ToArray(), _ => Inside);
        Converter converter = With(Ext("угадывающее", new PatternDetector([], Guess) { ByContent = true }), Ext("второй взгляд", inspector));

        Assert.Equal(Inside, await converter.DetectAsync(File("INSP")));
    }

    [Fact]
    public async Task SecondLook_IsSkippedForAnInputThatCannotSeek()
    {
        InspectingDetector inspector = new("INSP"u8.ToArray(), _ => Inside);
        DetectorChain chain = new([inspector]);
        await using InputHandle input = new("test", new ForwardReadStream("INSP ..."u8.ToArray()), ownsInner: true);

        Assert.Equal(FileFormat.Unknown, await InputDetection.DetectAsync(input, chain, CancellationToken.None));
        Assert.Equal(0, inspector.InspectCalls);
    }

    [Fact]
    public async Task SecondLook_ReadingPastTheBudget_IsNotMine()
    {
        byte[] bytes = [.. "INSP"u8.ToArray(), .. new byte[20_000]];
        InspectingDetector greedy = new("INSP"u8.ToArray(), stream => { stream.ReadExactly(new byte[10_000]); return Inside; });

        Assert.Equal(Inside, await Detect(new DetectorChain([greedy], inspectBudget: 16 * 1024), bytes));
        Assert.NotEqual(Inside, await Detect(new DetectorChain([greedy], inspectBudget: 4096), bytes));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidDataException))]
    public async Task SecondLook_ThatFailsToReadTheContainer_IsNotMine(Type failure)
    {
        InspectingDetector broken = new("INSP"u8.ToArray(), _ => throw (Exception)Activator.CreateInstance(failure, "сломан")!);

        Assert.NotEqual(Inside, await Detect(new DetectorChain([broken]), "INSP ..."u8.ToArray()));
        Assert.Equal(1, broken.InspectCalls);
    }

    [Fact]
    public async Task OneInstanceNamedTwice_IsAskedOnce()
    {
        PatternDetector shared = new("NEVER"u8.ToArray(), Label);
        Converter converter = With(Ext("первое", shared), Ext("второе", shared));

        await converter.DetectAsync(File("другое"));

        Assert.Equal(1, shared.Calls);
    }

    [Fact]
    public async Task SecondLook_AsksOnlyTheInspectorsThatCanInspectTheInput()
    {
        InspectingDetector foreign = new("OLEX"u8.ToArray(), _ => Guess);
        InspectingDetector fitting = new("INSP"u8.ToArray(), _ => Inside);

        Assert.Equal(Inside, await Detect(new DetectorChain([foreign, fitting]), "INSP ..."u8.ToArray()));
        Assert.Equal(0, foreign.InspectCalls);
        Assert.Equal(1, fitting.InspectCalls);
    }

    [Fact]
    public async Task HeadIsAtLeastAsLongAsTheDetectionWindow()
    {
        PatternDetector far = new("FAR"u8.ToArray(), Label, headLength: 8, offset: 10_000);
        byte[] bytes = [.. new byte[10_000], .. "FAR"u8.ToArray()];

        Assert.Equal(Label, await Detect(new DetectorChain([far], window: 20_000), bytes));
        Assert.NotEqual(Label, await Detect(new DetectorChain([far], window: 5_000), bytes));
    }

    [Fact]
    public async Task HeadIsAtLeastAsLongAsTheLongestHeadLength()
    {
        PatternDetector far = new("FAR"u8.ToArray(), Label, headLength: 200_000, offset: 100_000);
        byte[] bytes = [.. new byte[100_000], .. "FAR"u8.ToArray()];

        Assert.Equal(Label, await Detect(new DetectorChain([far]), bytes));
    }
}
