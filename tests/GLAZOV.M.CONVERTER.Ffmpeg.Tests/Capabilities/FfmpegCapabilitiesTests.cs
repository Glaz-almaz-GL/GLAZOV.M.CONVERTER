using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Capabilities;

/// <summary>
/// The catalog of a converter with the FFmpeg extension: the pairs of the three engines next to the built-in document ones.
/// </summary>
public sealed class FfmpegCapabilitiesTests
{
    private static Converter WithFfmpeg()
    {
        ConverterSettings settings = new();
        settings.Extensions.Add(new FfmpegExtension());
        return new Converter(settings);
    }

    [Fact]
    public void Converter_ListsAllPairs_AndKnowsFfmpeg()
    {
        ConverterCapabilities caps = WithFfmpeg().Capabilities;

        IReadOnlyList<Conversion> all = caps.All;

        Assert.Equal(165, all.Count);
        Assert.True(caps.CanConvert(FileFormat.Ogg, FileFormat.Mp3));
        Assert.False(caps.CanConvert(FileFormat.Ogg, FileFormat.Jpeg));
        Assert.True(caps.CanConvert(FileFormat.Png, FileFormat.Jpeg));
        Assert.Equal("ffmpeg", caps.Tool("ffmpeg").Name);
    }

    [Fact]
    public void Engines_FindAnEngineForAllTwentyFiveAudioPairs()
    {
        FileFormat[] audio = [FileFormat.Mp3, FileFormat.Wav, FileFormat.Ogg, FileFormat.Flac, FileFormat.M4a];
        EngineRegistry registry = new(new FfmpegExtension().Engines);

        foreach (FileFormat from in audio)
        {
            foreach (FileFormat to in audio)
            {
                Assert.NotNull(registry.Find(from, to));
            }
        }
    }

    [Fact]
    public void Pairs_CountOnlyFfmpeg_AndAreDistinct()
    {
        EngineRegistry registry = new(new FfmpegExtension().Engines);

        var pairs = registry.Pairs();

        Assert.Equal(165, pairs.Count);
        Assert.All(pairs, p => Assert.Contains(p.Tools, t => t.Name == "ffmpeg"));
        Assert.Equal(pairs.Count, pairs.Select(p => (p.From, p.To)).Distinct().Count());
    }

    [Fact]
    public void WithoutTheExtension_NoPairIsDoneByFfmpeg()
    {
        ConverterCapabilities caps = new Converter().Capabilities;

        Assert.False(caps.CanConvert(FileFormat.Ogg, FileFormat.Mp3));
        Assert.False(caps.CanConvert(FileFormat.Png, FileFormat.Jpeg));
    }

    [ExternalFact]
    public void WithFfmpegInstalled_AllFfmpegPairsAreReady()
    {
        ConverterCapabilities caps = WithFfmpeg().Capabilities;

        Assert.All(caps.All.Where(c => !c.To.Equals(FileFormat.Pdf)), c => Assert.Equal(ConversionAvailability.Ready, c.Availability));
        Assert.True(caps.Tool("ffmpeg").Found);
    }
}
