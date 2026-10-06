using System.Buffers.Binary;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Live;

/// <summary>
/// Настоящие изображения для живого прогона: «фотография» 640×480 во всех форматах (создаёт настоящий FFmpeg), прозрачный PNG,
/// анимированный GIF, DDS и PSD (FFmpeg их не пишет — собраны по спецификации и читаются FFmpeg), испорченный и «лживый» файлы,
/// TGA (его по содержимому не определить), набор из 40 копий для проверки параллельности.
/// </summary>
public sealed class ImageFixture : IAsyncLifetime
{
    /// <summary>Вход прогона: путь и ожидаемый формат.</summary>
    public sealed record Input(string Path, FileFormat Format, int Width, int Height);

    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "convrun-img-" + Guid.NewGuid().ToString("N"));

    public string Ffmpeg { get; private set; } = string.Empty;

    public string Ffprobe { get; private set; } = string.Empty;

    public IReadOnlyList<Input> Inputs { get; private set; } = [];

    public Input Photo => Inputs[0];

    public Input Transparent => Inputs.Single(i => Path.GetFileName(i.Path).StartsWith("прозрачный", StringComparison.Ordinal));

    public string BrokenPng => Path.Combine(Folder, "битый.png");

    public string LiarJpg => Path.Combine(Folder, "лжец.jpg");

    public string TgaFile => Path.Combine(Folder, "фото.tga");

    public string BatchFolder => Path.Combine(Folder, "пакет");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Folder);
        Ffmpeg = new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg).Path
            ?? throw new InvalidOperationException("FFmpeg не найден.");
        Ffprobe = Path.Combine(Path.GetDirectoryName(Ffmpeg)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        const string photo = "testsrc2=size=640x480:rate=1";
        List<Input> inputs =
        [
            await Make("фото.png", FileFormat.Png, 640, 480, photo, 1),
            await Make("фото.jpg", FileFormat.Jpeg, 640, 480, photo, 1, "-q:v", "4"),
            await Make("фото.webp", FileFormat.Webp, 640, 480, photo, 1, "-c:v", "libwebp", "-quality", "80"),
            await Make("фото.bmp", FileFormat.Bmp, 640, 480, photo, 1),
            await Make("фото.tiff", FileFormat.Tiff, 640, 480, photo, 1, "-c:v", "tiff"),
            await Make("анимация.gif", FileFormat.Gif, 160, 120, "testsrc=size=160x120:rate=5:duration=1", 5),
            await Make("прозрачный.png", FileFormat.Png, 200, 100, "color=c=0x336699@0.4:s=200x100,format=rgba", 1, "-pix_fmt", "rgba"),
        ];
        await Make("фото.tga", FileFormat.Tga, 640, 480, photo, 1, "-c:v", "targa");

        string dds = Path.Combine(Folder, "текстура.dds");
        await File.WriteAllBytesAsync(dds, BuildDds(64, 64));
        inputs.Add(new Input(dds, FileFormat.Dds, 64, 64));
        string psd = Path.Combine(Folder, "макет.psd");
        await File.WriteAllBytesAsync(psd, BuildPsd(64, 64));
        inputs.Add(new Input(psd, FileFormat.Psd, 64, 64));
        Inputs = inputs;

        byte[] garbage = new byte[2000];
        new Random(2027).NextBytes(garbage);
        await File.WriteAllBytesAsync(BrokenPng, garbage);
        await File.WriteAllBytesAsync(LiarJpg, await File.ReadAllBytesAsync(Photo.Path));

        Directory.CreateDirectory(BatchFolder);
        for (int i = 0; i < 40; i++)
        {
            File.Copy(Photo.Path, Path.Combine(BatchFolder, $"к{i:00}.png"));
        }
    }

    public Task DisposeAsync()
    {
        Directory.Delete(Folder, recursive: true);
        return Task.CompletedTask;
    }

    // Кодек, ширина и высота первого видеопотока файла настоящим ffprobe.
    public async Task<(string Codec, int Width, int Height)> ProbeAsync(string path)
    {
        using MemoryStream output = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffprobe,
            ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height", "-of", "csv=s=,:p=0", path],
            StandardOutput: output));
        Assert.True(result.ExitCode == 0, result.ErrorOutput);
        string[] parts = System.Text.Encoding.UTF8.GetString(output.ToArray()).Trim().Split(',');
        return (parts[0], int.Parse(parts[1]), int.Parse(parts[2]));
    }

    // Первый пиксель файла как сырые RGB-байты (чтение настоящим FFmpeg).
    public async Task<byte[]> FirstPixelAsync(string path, string pixelFormat = "rgb24")
    {
        using MemoryStream pixels = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg,
            ["-v", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", pixelFormat, "pipe:1"],
            StandardOutput: pixels));
        Assert.Equal(0, result.ExitCode);
        return pixels.ToArray();
    }

    private async Task<Input> Make(string name, FileFormat format, int width, int height, string source, int frames, params string[] extra)
    {
        string path = Path.Combine(Folder, name);
        List<string> args = ["-v", "error", "-y", "-f", "lavfi", "-i", source, "-frames:v", frames.ToString(), .. extra, path];

        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(Ffmpeg, args));
        Assert.True(result.ExitCode == 0, $"Не удалось создать {name}: {result.ErrorOutput}");
        return new Input(path, format, width, height);
    }

    // DDS без сжатия, 32 бита BGRA, градиент.
    private static byte[] BuildDds(int width, int height)
    {
        byte[] file = new byte[128 + (width * height * 4)];
        "DDS "u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), 0x1007);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(20), (uint)(width * 4));
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(76), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(80), 0x41);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(88), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(92), 0xFF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(96), 0xFF00);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(100), 0xFF);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(104), 0xFF000000);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(108), 0x1000);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int o = 128 + (((y * width) + x) * 4);
                file[o] = (byte)(x * 255 / (width - 1));      // B
                file[o + 1] = (byte)(y * 255 / (height - 1)); // G
                file[o + 2] = 128;                            // R
                file[o + 3] = 255;                            // A
            }
        }

        return file;
    }

    // PSD без сжатия, RGB, 8 бит на канал, градиент.
    private static byte[] BuildPsd(int width, int height)
    {
        int plane = width * height;
        byte[] file = new byte[26 + 4 + 4 + 4 + 2 + (plane * 3)];
        "8BPS"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(12), 3);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(14), (uint)height);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(18), (uint)width);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(22), 8);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(24), 3);
        int offset = 26 + 4 + 4 + 4;
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(offset), 0);
        offset += 2;
        for (int channel = 0; channel < 3; channel++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    file[offset++] = channel switch
                    {
                        0 => (byte)(x * 255 / (width - 1)),
                        1 => (byte)(y * 255 / (height - 1)),
                        _ => 128,
                    };
                }
            }
        }

        return file;
    }
}
