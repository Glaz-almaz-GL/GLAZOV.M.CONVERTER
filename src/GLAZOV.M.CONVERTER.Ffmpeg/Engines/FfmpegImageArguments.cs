using System.Globalization;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Engines;

/// <summary>
/// Сборка аргументов FFmpeg для изображений: чистая функция без запуска программы.
/// Вход и выход — пути к файлам (форматы TIFF, DDS и PSD не читаются из канала). Всегда один кадр.
/// </summary>
internal static class FfmpegImageArguments
{
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
    /// <param name="settings">Default quality and background; <c>null</c> — the default settings.</param>
    internal static IReadOnlyList<string> Build(FileFormat to, int? quality, string inputPath, string outputPath, IReadOnlyList<string>? filters = null, FfmpegSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        settings ??= new FfmpegSettings();

        List<string> chain = [.. filters ?? []];
        List<string> codec;
        string muxer = "image2";

        if (to.Equals(FileFormat.Png))
        {
            codec = ["-c:v", "png"];
        }
        else if (to.Equals(FileFormat.Jpeg))
        {
            // JPEG keeps no transparency: the picture is laid over the background colour.
            chain.Add($"split=2[bg][fg];[bg]drawbox=c={ColorText(settings.FlattenBackground)}@1:replace=1:t=fill[bg];[bg][fg]overlay=format=auto");
            codec = ["-c:v", "mjpeg", "-q:v", JpegQscale(quality ?? settings.JpegQuality), "-pix_fmt", "yuvj420p"];
        }
        else if (to.Equals(FileFormat.Webp))
        {
            codec = ["-c:v", "libwebp", "-quality", (quality ?? settings.WebpQuality).ToString(CultureInfo.InvariantCulture)];
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

    /// <summary>
    /// Переводит действия в цепочку фильтров FFmpeg по порядку действий: поворот вправо — <c>transpose=1</c>,
    /// масштаб в процентах — <c>scale</c> с округлением вниз и размером не меньше одного пикселя.
    /// </summary>
    /// <param name="operations">Действия по порядку; пусто или <c>null</c> — без фильтров.</param>
    internal static IReadOnlyList<string> Filters(IEnumerable<Operation>? operations)
    {
        List<string> filters = [];
        foreach (Operation operation in operations ?? [])
        {
            if (operation.Kind == OperationKind.RotateRight)
            {
                filters.Add("transpose=1");
            }
            else if (operation.Kind == OperationKind.ScalePercent && operation.Percent is int percent)
            {
                string p = percent.ToString(CultureInfo.InvariantCulture);
                filters.Add($"scale=w='max(1,trunc(iw*{p}/100))':h='max(1,trunc(ih*{p}/100))'");
            }
            else
            {
                throw new ArgumentException($"Действие «{operation.Kind}» движок изображений не умеет.", nameof(operations));
            }
        }

        return filters;
    }

    // Opaque RGB only: the alpha of the colour is ignored. White keeps its name, the rest goes as hex.
    private static string ColorText(System.Drawing.Color color)
    {
        return color.R == 255 && color.G == 255 && color.B == 255 ? "white" : $"0x{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    // Шкала качества JPEG у FFmpeg обратная: 2 — лучшее, 31 — худшее. Переводим 1–100 в 31…2.
    private static string JpegQscale(int quality)
    {
        int scale = 31 - (int)Math.Round((quality - 1) * 29.0 / 99.0);
        return Math.Clamp(scale, 2, 31).ToString(CultureInfo.InvariantCulture);
    }
}
