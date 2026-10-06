using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Tests.Engines;

/// <summary>
/// Реестр движков: подбор по паре, перечень пар для каталога; у ядра своих движков нет.
/// </summary>
public class EngineRegistryTests
{
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
    public void Default_HasNoEngines_TheCoreCarriesNone()
    {
        EngineRegistry registry = EngineRegistry.CreateDefault();

        Assert.Empty(registry.Engines);
        Assert.Empty(registry.Pairs());
    }

    [Fact]
    public void Pairs_ListEveryPairOnce_WithTheEngineThatWins_InStableOrder()
    {
        EngineRegistry registry = new([new FakeEngine("первая"), new FakeEngine("вторая")]);

        IReadOnlyList<(FileFormat From, FileFormat To, IReadOnlyList<ToolSpec> Tools)> pairs = registry.Pairs();

        Assert.Single(pairs);
        Assert.Equal((FileFormat.Mp3, FileFormat.Wav), (pairs[0].From, pairs[0].To));
        Assert.Equal(pairs, registry.Pairs());
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
