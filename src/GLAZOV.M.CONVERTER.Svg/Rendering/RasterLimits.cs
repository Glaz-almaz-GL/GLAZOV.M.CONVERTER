using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Svg.Rendering;

/// <summary>
/// Неизменяемая копия настроек движка SVG, снятая при создании расширения.
/// </summary>
/// <param name="MaxPixels">Наибольшее число пикселей результата.</param>
/// <param name="MaxInputBytes">Наибольший размер входа в байтах.</param>
/// <param name="DefaultJpegQuality">Качество JPEG, если в вызове не задано.</param>
/// <param name="DefaultWebpQuality">Качество WebP, если в вызове не задано.</param>
/// <param name="JpegBackground">Подложка под прозрачные места для JPEG, непрозрачная.</param>
internal sealed record RasterLimits(
    long MaxPixels,
    long MaxInputBytes,
    int DefaultJpegQuality,
    int DefaultWebpQuality,
    SKColor JpegBackground)
{
    /// <summary>
    /// Снимает копию с настроек.
    /// </summary>
    /// <param name="settings">Настройки движка SVG.</param>
    internal static RasterLimits From(SvgSettings settings)
    {
        return new RasterLimits(
            settings.MaxPixels,
            settings.MaxInputBytes,
            settings.DefaultJpegQuality,
            settings.DefaultWebpQuality,
            new SKColor(settings.JpegBackground.R, settings.JpegBackground.G, settings.JpegBackground.B));
    }
}
