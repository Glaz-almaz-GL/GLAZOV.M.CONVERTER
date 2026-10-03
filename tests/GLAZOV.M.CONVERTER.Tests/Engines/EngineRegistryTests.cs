using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;

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

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    [InlineData(4, 1)]
    public void Default_DoesNotFind_ImagesUnknownAndForeign(int fromIndex, int toIndex)
    {
        FileFormat[] all = [FileFormat.Jpeg, FileFormat.Mp3, FileFormat.Wav, FileFormat.Unknown, FileFormat.Create("DEMO", "application/x-demo", "demo")];
        FileFormat from = all[fromIndex];
        FileFormat to = all[toIndex];

        // Пары вне звука первой версии: JPEG, «неизвестно» и чужой формат движок не берёт.
        bool involvesNonAudio = !Audio.Contains(from) || !Audio.Contains(to);
        Assert.True(involvesNonAudio);
        Assert.Null(EngineRegistry.CreateDefault().Find(from, to));
    }

    [Fact]
    public void Pairs_ListTwentyFivePairsWithToolName_InStableOrder()
    {
        IReadOnlyList<(FileFormat From, FileFormat To, string ToolName)> first = EngineRegistry.CreateDefault().Pairs();
        IReadOnlyList<(FileFormat From, FileFormat To, string ToolName)> second = EngineRegistry.CreateDefault().Pairs();

        Assert.Equal(25, first.Count);
        Assert.All(first, p => Assert.Equal("ffmpeg", p.ToolName));
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

        public IReadOnlyList<(FileFormat From, FileFormat To)> Pairs { get; } = [(FileFormat.Mp3, FileFormat.Wav)];

        public bool CanConvert(FileFormat from, FileFormat to)
        {
            return from.Equals(FileFormat.Mp3) && to.Equals(FileFormat.Wav);
        }

        public Task ConvertAsync(EngineRequest request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
