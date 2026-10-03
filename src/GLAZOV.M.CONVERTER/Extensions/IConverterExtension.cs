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
    /// Определители формата по начальным байтам, в порядке опроса.
    /// </summary>
    IReadOnlyList<IFormatDetector> Detectors { get; }

    /// <summary>
    /// Движки расширения, в порядке подбора.
    /// </summary>
    IReadOnlyList<IConversionEngine> Engines { get; }
}
