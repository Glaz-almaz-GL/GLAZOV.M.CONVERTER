using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Расширение библиотеки: набор определителей формата и движков, который сторонний пакет подключает к <c>Converter</c>
/// настройкой <c>ConverterSettings.Extensions</c>. Подключается только при создании конвертера и дальше не меняется.
/// Расширение выполняется в процессе вызывающего: подключайте только то, чему доверяете.
/// </summary>
public interface IConverterExtension
{
    /// <summary>
    /// Имя расширения — непустое и уникальное среди подключённых (без учёта регистра).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Форматы, которые расширение объявляет. По умолчанию — различные форматы пар его движков, в порядке пар;
    /// формат, который расширение только определяет, перечисляется явно.
    /// </summary>
    IReadOnlyList<FileFormat> Formats => [.. Engines.SelectMany(engine => engine.Pairs).SelectMany(pair => new[] { pair.From, pair.To }).Distinct()];

    /// <summary>
    /// Определители формата по начальным байтам, в порядке опроса.
    /// </summary>
    IReadOnlyList<IFormatDetector> Detectors { get; }

    /// <summary>
    /// Движки расширения, в порядке подбора.
    /// </summary>
    IReadOnlyList<IConversionEngine> Engines { get; }
}
