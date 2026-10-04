using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Engines;

/// <summary>
/// Real video files made by FFmpeg for the video-to-audio checks: five containers with picture and tone,
/// one without sound and one long file (for progress and cancellation). One set for the whole class.
/// </summary>
public sealed class VideoToAudioFixture : IAsyncLifetime
{
    public const double ShortSeconds = 2;

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "convvideo-" + Guid.NewGuid().ToString("N"));

    public string Ffmpeg { get; private set; } = string.Empty;

    public string Ffprobe { get; private set; } = string.Empty;

    public string NoSound => Path.Combine(Folder, "no-sound.mp4");

    public string Long => Path.Combine(Folder, "long.mkv");

    public string Source(string extension)
    {
        return Path.Combine(Folder, "source." + extension);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Folder);
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));
        Ffmpeg = locator.Locate("ffmpeg").Path ?? throw new InvalidOperationException("FFmpeg не найден.");
        Ffprobe = Path.Combine(Path.GetDirectoryName(Ffmpeg)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        await Make("mp4", ["-c:v", "libx264", "-c:a", "aac"]);
        await Make("mkv", ["-c:v", "libx264", "-c:a", "aac"]);
        await Make("webm", ["-c:v", "libvpx-vp9", "-c:a", "libopus"]);
        await Make("mov", ["-c:v", "libx264", "-c:a", "aac"]);
        await Make("avi", ["-c:v", "mpeg4", "-c:a", "libmp3lame"]);

        await Run(["-f", "lavfi", "-i", $"testsrc=size=64x64:rate=10:duration={ShortSeconds}", "-c:v", "libx264", NoSound]);
        await Run([
            "-f", "lavfi", "-i", "testsrc=size=64x64:rate=1:duration=3000",
            "-f", "lavfi", "-i", "sine=frequency=330:duration=3000",
            "-c:v", "mpeg4", "-c:a", "aac", Long]);
    }

    public Task DisposeAsync()
    {
        Directory.Delete(Folder, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads one value of a file with ffprobe: <c>format=duration</c>, <c>stream=codec_name</c>, and so on.
    /// </summary>
    public async Task<string> ProbeAsync(string path, string entry)
    {
        using MemoryStream output = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffprobe,
            ["-v", "error", "-select_streams", "a:0", "-show_entries", entry, "-of", "default=nw=1:nk=1", path],
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

    private async Task Run(string[] args)
    {
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(Ffmpeg, ["-v", "error", "-y", .. args]));
        Assert.Equal(0, result.ExitCode);
    }
}
