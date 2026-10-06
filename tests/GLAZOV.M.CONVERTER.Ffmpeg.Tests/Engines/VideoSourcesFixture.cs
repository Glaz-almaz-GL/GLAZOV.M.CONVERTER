using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Engines;

/// <summary>
/// Real video files made by FFmpeg for the video checks: five small containers with picture and tone,
/// five 640x480 ones, one without sound, one without picture, a noisy one (for bitrate) and one long file
/// (for progress and cancellation). Built once per test run and shared by every class that asks for it;
/// removed when the run ends.
/// </summary>
public sealed class VideoSourcesFixture : IAsyncLifetime
{
    public const double ShortSeconds = 2;

    private static readonly string SharedFolder = Path.Combine(Path.GetTempPath(), "convvideo-" + Guid.NewGuid().ToString("N"));
    private static readonly object Gate = new();
    private static Task? _building;
    private static string _ffmpeg = string.Empty;
    private static string _ffprobe = string.Empty;

    public string Folder => SharedFolder;

    public string Ffmpeg => _ffmpeg;

    public string Ffprobe => _ffprobe;

    public string NoSound => Path.Combine(Folder, "no-sound.mp4");

    public string NoPicture => Path.Combine(Folder, "no-picture.mkv");

    /// <summary>
    /// A 640x480 source of ten seconds: H.264 and AAC at 64 kbit/s (the engine default would be 128), for copying checks.
    /// </summary>
    public string Ten(string extension)
    {
        return Path.Combine(Folder, "ten." + extension);
    }

    public string Noisy => Path.Combine(Folder, "noisy.mp4");

    public string Long => Path.Combine(Folder, "long.mkv");

    /// <summary>
    /// A 640x480 source of three seconds with picture and tone.
    /// </summary>
    public string Rich(string extension)
    {
        return Path.Combine(Folder, "rich." + extension);
    }

    public string Source(string extension)
    {
        return Path.Combine(Folder, "source." + extension);
    }

    public Task InitializeAsync()
    {
        lock (Gate)
        {
            if (_building is null)
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                {
                    if (Directory.Exists(SharedFolder))
                    {
                        Directory.Delete(SharedFolder, recursive: true);
                    }
                };
                _building = BuildAsync();
            }

            return _building;
        }
    }

    private async Task BuildAsync()
    {
        Directory.CreateDirectory(Folder);
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));
        _ffmpeg = locator.Locate(FfmpegTools.Ffmpeg).Path ?? throw new InvalidOperationException("FFmpeg не найден.");
        _ffprobe = Path.Combine(Path.GetDirectoryName(_ffmpeg)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        await Make("mp4", ["-c:v", "libx264", "-c:a", "aac"]);
        await Make("mkv", ["-c:v", "libx264", "-c:a", "aac"]);
        await Make("webm", ["-c:v", "libvpx-vp9", "-c:a", "libopus"]);
        await Make("mov", ["-c:v", "libx264", "-c:a", "aac"]);
        await Make("avi", ["-c:v", "mpeg4", "-c:a", "libmp3lame"]);

        await MakeRich("mp4", ["-c:v", "libx264", "-c:a", "aac"]);
        await MakeRich("mkv", ["-c:v", "libx264", "-c:a", "aac"]);
        await MakeRich("webm", ["-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", "-c:a", "libopus"]);
        await MakeRich("mov", ["-c:v", "libx264", "-c:a", "aac"]);
        await MakeRich("avi", ["-c:v", "mpeg4", "-c:a", "libmp3lame"]);

        await Run(["-f", "lavfi", "-i", "testsrc=size=640x480:rate=10:duration=6,noise=alls=40:allf=t+u", "-c:v", "libx264", "-crf", "10", Noisy]);
        await Run(["-f", "lavfi", "-i", $"sine=frequency=440:duration={ShortSeconds}", "-c:a", "libopus", NoPicture]);
        foreach (string extension in new[] { "mkv", "mp4" })
        {
            await Run([
                "-f", "lavfi", "-i", "testsrc=size=640x480:rate=25:duration=10",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=10",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "64k", Ten(extension)]);
        }

        await Run(["-f", "lavfi", "-i", $"testsrc=size=64x64:rate=10:duration={ShortSeconds}", "-c:v", "libx264", NoSound]);
        await Run([
            "-f", "lavfi", "-i", "testsrc=size=64x64:rate=1:duration=900",
            "-f", "lavfi", "-i", "sine=frequency=330:duration=900",
            "-c:v", "mpeg4", "-c:a", "aac", Long]);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads one value of a file with ffprobe: <c>format=duration</c>, <c>stream=codec_name</c>, and so on.
    /// </summary>
    public async Task<string> ProbeAsync(string path, string entry, string stream = "a:0")
    {
        using MemoryStream output = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffprobe,
            ["-v", "error", "-select_streams", stream, "-show_entries", entry, "-of", "default=nw=1:nk=1", path],
            StandardOutput: output));
        Assert.Equal(0, result.ExitCode);

        return System.Text.Encoding.UTF8.GetString(output.ToArray()).Trim();
    }

    private Task Make(string extension, string[] codecs)
    {
        return Run([
            "-f", "lavfi", "-i", $"testsrc=size=64x64:rate=10:duration={ShortSeconds}",
            "-f", "lavfi", "-i", $"sine=frequency=440:duration={ShortSeconds}",
            .. codecs, Source(extension)]);
    }

    private Task MakeRich(string extension, string[] codecs)
    {
        return Run([
            "-f", "lavfi", "-i", $"testsrc=size=640x480:rate=10:duration=3",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=3",
            .. codecs, Rich(extension)]);
    }

    private async Task Run(string[] args)
    {
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(Ffmpeg, ["-v", "error", "-y", .. args]));
        Assert.Equal(0, result.ExitCode);
    }
}
