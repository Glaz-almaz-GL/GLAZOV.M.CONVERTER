using System.Globalization;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Live;

/// <summary>
/// Настоящие звуковые файлы для живого прогона: создаются настоящим FFmpeg (разные форматы, длительности, частоты,
/// каналы), плюс испорченный файл и файл с чужим расширением. Один набор на весь класс проверок.
/// </summary>
public sealed class AudioFixture : IAsyncLifetime
{
    /// <summary>Описание входа: имя файла, ожидаемая длительность, ожидаемое имя типа.</summary>
    public sealed record Input(string Path, double Seconds, string TypeName);

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "convrun-" + Guid.NewGuid().ToString("N"));

    public string Ffmpeg { get; private set; } = string.Empty;

    public string Ffprobe { get; private set; } = string.Empty;

    public IReadOnlyList<Input> Inputs { get; private set; } = [];

    public string BrokenMp3 => Path.Combine(Folder, "broken.mp3");

    public string LiarOgg => Path.Combine(Folder, "liar.ogg");

    public string BatchFolder => Path.Combine(Folder, "batch");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Folder);
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));
        Ffmpeg = locator.Locate("ffmpeg").Path ?? throw new InvalidOperationException("FFmpeg не найден.");
        Ffprobe = Path.Combine(Path.GetDirectoryName(Ffmpeg)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        List<Input> inputs = [];
        inputs.Add(await Make("mp3-стерео-1с.mp3", 1, 44100, 2, 440, ["-c:a", "libmp3lame", "-b:a", "128k"], "MP3"));
        inputs.Add(await Make("mp3-моно-4с.mp3", 4, 22050, 1, 880, ["-c:a", "libmp3lame", "-q:a", "5"], "MP3"));
        inputs.Add(await Make("wav-стерео-1с.wav", 1, 44100, 2, 330, ["-c:a", "pcm_s16le"], "WAV"));
        inputs.Add(await Make("wav-моно-4с.wav", 4, 16000, 1, 660, ["-c:a", "pcm_s16le"], "WAV"));
        inputs.Add(await Make("vorbis-стерео-1с.ogg", 1, 44100, 2, 550, ["-c:a", "libvorbis", "-q:a", "4"], "OGG"));
        inputs.Add(await Make("vorbis-моно-4с.ogg", 4, 22050, 1, 770, ["-c:a", "libvorbis", "-q:a", "2"], "OGG"));
        inputs.Add(await Make("opus-стерео-1с.ogg", 1, 48000, 2, 500, ["-c:a", "libopus", "-b:a", "64k"], "OGG"));
        inputs.Add(await Make("opus-моно-4с.ogg", 4, 48000, 1, 700, ["-c:a", "libopus", "-b:a", "32k"], "OGG"));
        Inputs = inputs;

        byte[] garbage = new byte[3000];
        new Random(2026).NextBytes(garbage);
        await File.WriteAllBytesAsync(BrokenMp3, garbage);
        await File.WriteAllBytesAsync(LiarOgg, await File.ReadAllBytesAsync(inputs[0].Path));

        // Набор для проверки параллельности: один WAV в 40 копиях под разными именами.
        Input basis = await Make("основа.wav", 60, 8000, 1, 400, ["-c:a", "pcm_s16le"], "WAV");
        Directory.CreateDirectory(BatchFolder);
        for (int i = 0; i < 40; i++)
        {
            File.Copy(basis.Path, Path.Combine(BatchFolder, $"b{i:00}.wav"));
        }

        File.Delete(basis.Path);
    }

    public Task DisposeAsync()
    {
        Directory.Delete(Folder, recursive: true);
        return Task.CompletedTask;
    }

    // Разбор файла настоящим ffprobe по пути (по каналу длительность у части форматов неизвестна): длительность, частота, каналы, битрейт.
    public async Task<Dictionary<string, string>> ProbeFileAsync(string path)
    {
        using MemoryStream output = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffprobe,
            ["-v", "error", "-show_entries", "format=duration,bit_rate:stream=sample_rate,channels", "-of", "default=nw=1", path],
            StandardOutput: output));
        Assert.Equal(0, result.ExitCode);

        return System.Text.Encoding.UTF8.GetString(output.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0])
            .ToDictionary(group => group.Key, group => group.First()[1]);
    }

    // То же для байтов результата: сначала во временный файл.
    public async Task<Dictionary<string, string>> ProbeAsync(Stream data)
    {
        string temp = Path.Combine(Folder, "разбор-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await using (FileStream file = File.Create(temp))
            {
                await data.CopyToAsync(file);
            }

            return await ProbeFileAsync(temp);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private async Task<Input> Make(string name, double seconds, int rate, int channels, int frequency, string[] codec, string typeName)
    {
        string path = Path.Combine(Folder, name);
        string source = $"sine=frequency={frequency}:duration={seconds.ToString(CultureInfo.InvariantCulture)}:sample_rate={rate}";
        List<string> args = ["-v", "error", "-y", "-f", "lavfi", "-i", source, "-ac", channels.ToString(CultureInfo.InvariantCulture), .. codec, path];
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(Ffmpeg, args));
        Assert.True(result.ExitCode == 0, $"Не удалось создать {name}: {result.ErrorOutput}");
        return new Input(path, seconds, typeName);
    }
}
