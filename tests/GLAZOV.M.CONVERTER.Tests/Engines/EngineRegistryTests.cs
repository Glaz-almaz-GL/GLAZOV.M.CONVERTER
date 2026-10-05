using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Engines;

/// <summary>
/// Реестр движков: подбор по паре, перечень пар для каталога, пары звукового движка.
/// </summary>
public class EngineRegistryTests
{
    private static readonly FileFormat[] Audio = [FileFormat.Mp3, FileFormat.Wav, FileFormat.Ogg, FileFormat.Flac, FileFormat.M4a];

    [Fact]
    public void Default_FindsAnEngineForAllTwentyFiveAudioPairs()
    {
        EngineRegistry registry = EngineRegistry.CreateDefault();

        foreach (FileFormat from in Audio)
        {
            foreach (FileFormat to in Audio)
            {
                Assert.NotNull(registry.Find(from, to));
            }
        }
    }

    [Fact]
    public void Default_DoesNotFind_UnknownForeignOrCrossKindPairs()
    {
        FileFormat demo = FileFormat.Create("DEMO", "application/x-demo", "demo");
        EngineRegistry registry = EngineRegistry.CreateDefault();

        Assert.Null(registry.Find(FileFormat.Unknown, FileFormat.Mp3));
        Assert.Null(registry.Find(FileFormat.Mp3, FileFormat.Unknown));
        Assert.Null(registry.Find(demo, FileFormat.Wav));
        Assert.Null(registry.Find(FileFormat.Wav, demo));
        Assert.Null(registry.Find(FileFormat.Mp3, FileFormat.Png));
        Assert.Null(registry.Find(FileFormat.Png, FileFormat.Mp3));
        Assert.Null(registry.Find(FileFormat.Svg, FileFormat.Png));
        Assert.Null(registry.Find(FileFormat.Png, FileFormat.Dds));
    }

    [Fact]
    public void Pairs_ListAllPairsWithToolName_InStableOrder()
    {
        IReadOnlyList<(FileFormat From, FileFormat To, string? ToolName)> first = EngineRegistry.CreateDefault().Pairs();
        IReadOnlyList<(FileFormat From, FileFormat To, string? ToolName)> second = EngineRegistry.CreateDefault().Pairs();

        Assert.Equal(142, first.Count);
        Assert.Equal(131, first.Count(p => p.ToolName == "ffmpeg"));
        Assert.Equal(11, first.Count(p => p.ToolName == "soffice"));
        Assert.All(first.Where(p => p.ToolName == "soffice"), p => Assert.Equal(FileFormat.Pdf, p.To));
        Assert.Equal(first, second);
        Assert.Equal(first.Select(p => (p.From, p.To)).Distinct().Count(), first.Count);
    }

    [Fact]
    public void FirstEngineThatCanConvert_Wins()
    {
        FakeEngine first = new("первая");
        FakeEngine second = new("вторая");
        EngineRegistry registry = new([first, second]);

        Assert.Same(first, registry.Find(FileFormat.Mp3, FileFormat.Wav));
    }

    private sealed class FakeEngine(string toolName) : IConversionEngine
    {
        public string ToolName { get; } = toolName;

        public bool SupportsOperations => false;

        public IReadOnlyList<ConversionPair> Pairs { get; } = [new(FileFormat.Mp3, FileFormat.Wav)];

        public bool CanConvert(FileFormat from, FileFormat to)
        {
            return from.Equals(FileFormat.Mp3) && to.Equals(FileFormat.Wav);
        }

        public Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
