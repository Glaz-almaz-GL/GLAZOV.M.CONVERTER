using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Svg.Engines;
using GLAZOV.M.CONVERTER.Svg.Rendering;

namespace GLAZOV.M.CONVERTER.Svg;

/// <summary>
/// Расширение «SVG»: растеризует SVG в PNG, JPEG и WebP, выполняет поворот и масштаб перерисовкой.
/// Подключается настройкой <c>ConverterSettings.Extensions</c>. Внешних программ не требует;
/// внешние ссылки внутри SVG (файлы, адреса сети) не загружаются — рисуется только сам документ.
/// </summary>
public sealed class SvgExtension : IConverterExtension
{
    /// <summary>
    /// Создаёт расширение с настройками по умолчанию.
    /// </summary>
    public SvgExtension()
        : this(new SvgSettings())
    {
    }

    /// <summary>
    /// Создаёт расширение с заданными настройками; их значения копируются,
    /// дальнейшие изменения объекта настроек на расширение не влияют.
    /// </summary>
    /// <param name="settings">Настройки движка SVG.</param>
    public SvgExtension(SvgSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Engines = [new SvgEngine(RasterLimits.From(settings))];
    }

    /// <inheritdoc />
    public string Name => "SVG";

    /// <inheritdoc />
    /// <remarks>
    /// Пусто: SVG определяют готовые признаки основной библиотеки.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; }
}
