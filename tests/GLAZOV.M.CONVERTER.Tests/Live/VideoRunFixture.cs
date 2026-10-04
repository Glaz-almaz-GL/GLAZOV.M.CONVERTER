using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Live;

/// <summary>
/// Real video files for the live run, made by real FFmpeg with different encoders and properties: H.264 and AAC in
/// MP4, MOV and MKV; VP9 and Opus in WebM; MPEG-4 and MP3 in AVI; no sound; sound only in an MP4; a phone video with a
/// rotation flag; an odd frame size 641x361; a variable frame rate; plus a long file, a broken one and a liar.
/// </summary>
public sealed class VideoRunFixture : IAsyncLifetime
{
    public const int Seconds = 10;

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "convvideorun-" + Guid.NewGuid().ToString("N"));

    public string Ffmpeg { get; private set; } = string.Empty;

    public string Ffprobe { get; private set; } = string.Empty;

    /// <summary>The five main inputs: container, path, video codec, audio codec.</summary>
    public IReadOnlyList<(string Container, string Path, string Video, string Audio)> Inputs { get; private set; } = [];

    public string NoSound => Path.Combine(Folder, "без-звука.mp4");

    public string SoundOnly => Path.Combine(Folder, "только-звук.mp4");

    public string Rotated => Path.Combine(Folder, "повёрнутое.mp4");

    public string Odd => Path.Combine(Folder, "нечётное-641x361.mp4");

    public string Variable => Path.Combine(Folder, "переменная-частота.mkv");

    public string Long => Path.Combine(Folder, "длинное.mkv");

    public string Broken => Path.Combine(Folder, "обрезанное.mp4");

    public string Liar => Path.Combine(Folder, "лжец.mp4");

    public string BatchFolder => Path.Combine(Folder, "пакет");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Folder);
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));
        Ffmpeg = locator.Locate("ffmpeg").Path ?? throw new InvalidOperationException("FFmpeg не найден.");
        Ffprobe = Path.Combine(Path.GetDirectoryName(Ffmpeg)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        string[] picture = ["-f", "lavfi", "-i", $"testsrc=size=320x240:rate=25:duration={Seconds}"];
        string[] tone = ["-f", "lavfi", "-i", $"sine=frequency=440:duration={Seconds}"];
        List<(string, string, string, string)> inputs = [];

        foreach (string container in new[] { "mp4", "mov", "mkv" })
        {
            string path = Path.Combine(Folder, $"h264-aac.{container}");
            await Run([.. picture, .. tone, "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", path]);
            inputs.Add((container, path, "h264", "aac"));
        }

        string webm = Path.Combine(Folder, "vp9-opus.webm");
        await Run([.. picture, .. tone, "-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", "-c:a", "libopus", webm]);
        inputs.Add(("webm", webm, "vp9", "opus"));

        string avi = Path.Combine(Folder, "mpeg4-mp3.avi");
        await Run([.. picture, .. tone, "-c:v", "mpeg4", "-c:a", "libmp3lame", avi]);
        inputs.Add(("avi", avi, "mpeg4", "mp3"));
        Inputs = inputs;

        await Run([.. picture, "-c:v", "libx264", "-pix_fmt", "yuv420p", NoSound]);
        await Run([.. tone, "-c:a", "aac", SoundOnly]);

        // The rotation flag is set on the input side, so the file is a copy of the pixels with a display matrix.
        string upright = Path.Combine(Folder, "прямое.mp4");
        await Run([.. picture, .. tone, "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", upright]);
        await Run(["-display_rotation", "90", "-i", upright, "-c", "copy", Rotated]);
        File.Delete(upright);

        // libx264 cannot write an odd frame, so an odd source is made with MPEG-4.
        await Run(["-f", "lavfi", "-i", $"testsrc=size=641x361:rate=25:duration={Seconds}", .. tone, "-c:v", "mpeg4", "-c:a", "aac", Odd]);

        // Frames are dropped at random and kept with their own times: the rate varies.
        await Run([.. picture, .. tone, "-vf", "select='gt(random(0),0.4)'", "-fps_mode", "vfr", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", Variable]);

        await Run(["-f", "lavfi", "-i", "testsrc=size=640x360:rate=25:duration=120", "-f", "lavfi", "-i", "sine=frequency=330:duration=120", "-c:v", "mpeg4", "-c:a", "aac", Long]);

        // Without +faststart an MP4 keeps its index at the end, so half of the file is not a file.
        byte[] whole = await File.ReadAllBytesAsync(inputs[0].Item2);
        await File.WriteAllBytesAsync(Broken, whole.AsMemory(0, whole.Length / 2).ToArray());
        await Run([.. tone, "-c:a", "libmp3lame", Path.Combine(Folder, "звук.mp3")]);
        File.Copy(Path.Combine(Folder, "звук.mp3"), Liar, overwrite: true);

        // Thirty small clips for the folder run.
        string clip = Path.Combine(Folder, "клип.avi");
        await Run(["-f", "lavfi", "-i", "testsrc=size=160x120:rate=25:duration=2", "-f", "lavfi", "-i", "sine=frequency=500:duration=2", "-c:v", "mpeg4", "-c:a", "libmp3lame", clip]);
        Directory.CreateDirectory(BatchFolder);
        for (int i = 0; i < 30; i++)
        {
            File.Copy(clip, Path.Combine(BatchFolder, $"клип{i:00}.avi"));
        }

        File.Delete(clip);
    }

    public Task DisposeAsync()
    {
        Directory.Delete(Folder, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// One value of the first stream of a kind, read with the real ffprobe.
    /// </summary>
    public async Task<string> ProbeAsync(string path, string entry, string stream = "v:0")
    {
        using MemoryStream output = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffprobe,
            ["-v", "error", "-select_streams", stream, "-show_entries", entry, "-of", "default=nw=1:nk=1", path],
            StandardOutput: output));
        Assert.Equal(0, result.ExitCode);
        return System.Text.Encoding.UTF8.GetString(output.ToArray()).Trim();
    }

    public async Task<string> SizeAsync(string path)
    {
        return $"{await ProbeAsync(path, "stream=width")}x{await ProbeAsync(path, "stream=height")}";
    }

    public async Task<double> DurationAsync(string path)
    {
        return double.Parse(await ProbeAsync(path, "format=duration"), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task Run(string[] args)
    {
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(Ffmpeg, ["-v", "error", "-y", .. args]));
        Assert.Equal(0, result.ExitCode);
    }
}
