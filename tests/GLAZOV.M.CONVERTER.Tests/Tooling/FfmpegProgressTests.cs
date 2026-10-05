using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Tooling;

/// <summary>
/// Percent from FFmpeg output: the parser on prepared lines, and on the real output of a ten-second file.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegProgressTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));

    public FfmpegProgressTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    private static List<int> Feed(params string[] lines)
    {
        List<int> percents = [];
        FfmpegProgress progress = new(percents.Add);
        foreach (string line in lines)
        {
            progress.ProcessLine(line);
        }

        return percents;
    }

    private static string Ffmpeg()
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg).Path!;
    }

    [Fact]
    public void DurationAndPositions_GiveGrowingPercentsCappedAt99()
    {
        List<int> percents = Feed(
            "  Duration: 00:00:10.00, start: 0.000000, bitrate: 128 kb/s",
            "out_time_us=1000000",
            "out_time_us=5000000",
            "out_time_us=5000000",
            "out_time_us=2000000",
            "out_time_us=10000000",
            "out_time_us=12000000");

        Assert.Equal([10, 50, 99], percents);
    }

    [Fact]
    public void PositionWithoutAKnownDuration_GivesNothing()
    {
        Assert.Empty(Feed("out_time_us=5000000"));
        Assert.Empty(Feed("  Duration: N/A, bitrate: N/A", "out_time_us=5000000"));
        Assert.Empty(Feed("  Duration: 00:00:00.00, start: 0.000000", "out_time_us=5000000"));
    }

    [Fact]
    public void NegativeNotAvailableAndJunkPositions_DoNotBreakIt()
    {
        List<int> percents = Feed(
            "  Duration: 00:01:40.50, start: 0.000000",
            "out_time_us=-9223372036854775807",
            "out_time_us=N/A",
            "out_time_us=",
            "out_time_us=abc",
            "out_time_us=99999999999999999999999",
            "out_time_us=50250000");

        Assert.Equal([50], percents);
    }

    [Fact]
    public void OnlyTheFirstDurationCounts_AndOtherLinesAreIgnored()
    {
        List<int> percents = Feed(
            "Input #0, matroska,webm, from 'x.mkv':",
            "  Duration: 00:00:10.00, start: 0.000000",
            "    Stream #0:0: Video: h264",
            "  Duration: 00:00:20.00, start: 0.000000",
            "frame=1",
            "out_time=00:00:05.000000",
            "out_time_us=5000000",
            "progress=continue");

        Assert.Equal([50], percents);
    }

    [Fact]
    public void LinesFromSeveralThreads_StayGrowing()
    {
        List<int> percents = [];
        FfmpegProgress progress = new(percent =>
        {
            lock (percents)
            {
                percents.Add(percent);
            }
        });
        progress.ProcessLine("  Duration: 00:00:10.00, start: 0.000000");

        Parallel.For(0, 200, i => progress.ProcessLine($"out_time_us={i * 50_000}"));

        Assert.Equal(percents.Order().ToArray(), percents);
        Assert.Equal(percents.Distinct().Count(), percents.Count);
    }

    [Fact]
    public void ProgressArguments_AreAddedInFront_WithoutChangingTheRest()
    {
        IReadOnlyList<string> original = ["-i", "in.mp4", "out.mkv"];

        Assert.Equal(["-progress", "pipe:1", "-nostats", "-i", "in.mp4", "out.mkv"], FfmpegProgressArguments.Add(original));
        Assert.Equal(["-progress", "pipe:2", "-nostats", "-i", "in.mp4", "out.mkv"], FfmpegProgressArguments.Add(original, "pipe:2"));
        Assert.Equal(["-i", "in.mp4", "out.mkv"], original);
    }

    [ExternalFact]
    public async Task RealFfmpeg_OnATenSecondFile_GivesGrowingPercentsBelow100()
    {
        string source = Path.Combine(_temp, "ten.avi");
        string target = Path.Combine(_temp, "out.avi");
        ToolResult made = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg(),
            ["-v", "error", "-f", "lavfi", "-i", "testsrc=size=64x64:rate=10:duration=10", "-c:v", "mpeg4", source]));
        Assert.Equal(0, made.ExitCode);

        List<int> percents = [];
        FfmpegProgress progress = new(percent =>
        {
            lock (percents)
            {
                percents.Add(percent);
            }
        });
        IReadOnlyList<string> args = FfmpegProgressArguments.Add(
            ["-readrate", "10", "-stats_period", "0.1", "-i", source, "-c:v", "mpeg4", "-y", target]);

        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg(), args, OnErrorLine: progress.ProcessLine, OnOutputLine: progress.ProcessLine));

        Assert.Equal(0, result.ExitCode);
        Assert.True(percents.Count >= 3, $"Too few percents: {string.Join(", ", percents)}");
        Assert.Equal(percents.Order().ToArray(), percents);
        Assert.Equal(percents.Distinct().Count(), percents.Count);
        Assert.InRange(percents[^1], 1, 99);
    }
}
