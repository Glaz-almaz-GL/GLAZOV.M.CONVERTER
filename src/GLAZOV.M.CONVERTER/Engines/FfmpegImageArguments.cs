using System.Globalization;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Сборка аргументов FFmpeg для изображений: чистая функция без запуска программы.
/// Вход и выход — пути к файлам (форматы TIFF, DDS и PSD не читаются из канала). Всегда один кадр.
/// </summary>
internal static class FfmpegImageArguments
{
    // Умолчания движка — параметры со значением, не зашитая логика: их перекрывает ImageOptions.Quality (шкала 1–100).
    private const int JpegDefaultQuality = 90;
    private const int WebpDefaultQuality = 80;

    // Прозрачность JPEG не хранит: изображение накладывается на белый фон.
    private const string FlattenOnWhite = "split=2[bg][fg];[bg]drawbox=c=white@1:replace=1:t=fill[bg];[bg][fg]overlay=format=auto";

    // Палитра GIF строится по самому кадру: без этого цвета теряются.
    private const string GifPalette = "split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse";

    /// <summary>
    /// Собирает аргументы: один кадр, фильтры и кодек по формату результата.
    /// </summary>
    /// <param name="to">Формат результата: PNG, JPEG, WebP, BMP, GIF, TIFF или TGA.</param>
    /// <param name="quality">Качество 1–100 для JPEG и WebP; пусто — умолчание движка; для остальных форматов не действует.</param>
    /// <param name="inputPath">Путь к файлу входа.</param>
    /// <param name="outputPath">Путь к файлу результата.</param>
    /// <param name="filters">Фильтры действий над изображением (поворот, масштаб) по порядку; пусто — без действий.</param>
    internal static IReadOnlyList<string> Build(FileFormat to, int? quality, string inputPath, string outputPath, IReadOnlyList<string>? filters = null)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        List<string> chain = [.. filters ?? []];
        List<string> codec;
        string muxer = "image2";

        if (to.Equals(FileFormat.Png))
        {
            codec = ["-c:v", "png"];
        }
        else if (to.Equals(FileFormat.Jpeg))
        {
            chain.Add(FlattenOnWhite);
            codec = ["-c:v", "mjpeg", "-q:v", JpegQscale(quality ?? JpegDefaultQuality), "-pix_fmt", "yuvj420p"];
        }
        else if (to.Equals(FileFormat.Webp))
        {
            codec = ["-c:v", "libwebp", "-quality", (quality ?? WebpDefaultQuality).ToString(CultureInfo.InvariantCulture)];
            muxer = "webp";
        }
        else if (to.Equals(FileFormat.Bmp))
        {
            codec = ["-c:v", "bmp"];
        }
        else if (to.Equals(FileFormat.Gif))
        {
            chain.Add(GifPalette);
            codec = ["-c:v", "gif"];
            muxer = "gif";
        }
        else if (to.Equals(FileFormat.Tiff))
        {
            codec = ["-c:v", "tiff"];
        }
        else if (to.Equals(FileFormat.Tga))
        {
            codec = ["-c:v", "targa"];
        }
        else
        {
            throw new ArgumentException($"Движок изображений не умеет формат результата «{to.Name}».", nameof(to));
        }

        List<string> args = ["-hide_banner", "-loglevel", "error", "-nostats", "-i", inputPath, "-frames:v", "1"];
        if (chain.Count > 0)
        {
            args.AddRange(["-vf", string.Join(',', chain)]);
        }

        args.AddRange(codec);
        if (muxer == "image2")
        {
            args.AddRange(["-update", "1"]);
        }

        args.AddRange(["-f", muxer, "-y", outputPath]);
        return args;
    }

    // Шкала качества JPEG у FFmpeg обратная: 2 — лучшее, 31 — худшее. Переводим 1–100 в 31…2.
    private static string JpegQscale(int quality)
    {
        int scale = 31 - (int)Math.Round((quality - 1) * 29.0 / 99.0);
        return Math.Clamp(scale, 2, 31).ToString(CultureInfo.InvariantCulture);
    }
}
