using System.Drawing;

namespace GLAZOV.M.CONVERTER.Svg;

/// <summary>
/// Настройки движка SVG. Передаются в <see cref="SvgExtension"/> при создании
/// и после этого не меняют его поведения: расширение берёт копию значений.
/// </summary>
public sealed class SvgSettings
{
    private long _maxPixels = 50_000_000;
    private long _maxInputBytes = 64L * 1024 * 1024;
    private int _defaultJpegQuality = 90;
    private int _defaultWebpQuality = 80;

    /// <summary>
    /// Наибольшее число пикселей результата (ширина × высота) после всех действий.
    /// Защищает память: SVG может объявить размер в сотни тысяч пикселей. По умолчанию 50 миллионов.
    /// </summary>
    public long MaxPixels
    {
        get => _maxPixels;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxPixels));
            _maxPixels = value;
        }
    }

    /// <summary>
    /// Наибольший размер входа SVG в байтах. По умолчанию 64 МБ.
    /// </summary>
    public long MaxInputBytes
    {
        get => _maxInputBytes;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxInputBytes));
            _maxInputBytes = value;
        }
    }

    /// <summary>
    /// Качество JPEG от 1 до 100, если в вызове не задано <c>ImageOptions.Quality</c>. По умолчанию 90.
    /// </summary>
    public int DefaultJpegQuality
    {
        get => _defaultJpegQuality;
        set => _defaultJpegQuality = CheckQuality(value, nameof(DefaultJpegQuality));
    }

    /// <summary>
    /// Качество WebP от 1 до 100, если в вызове не задано <c>ImageOptions.Quality</c>. По умолчанию 80.
    /// </summary>
    public int DefaultWebpQuality
    {
        get => _defaultWebpQuality;
        set => _defaultWebpQuality = CheckQuality(value, nameof(DefaultWebpQuality));
    }

    /// <summary>
    /// Цвет подложки под прозрачные места для JPEG, который прозрачность не хранит.
    /// Прозрачность самой подложки не учитывается. По умолчанию белый.
    /// </summary>
    public Color JpegBackground { get; set; } = Color.White;

    private static int CheckQuality(int value, string name)
    {
        if (value is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, value, "Качество изображения — число от 1 до 100.");
        }

        return value;
    }
}
