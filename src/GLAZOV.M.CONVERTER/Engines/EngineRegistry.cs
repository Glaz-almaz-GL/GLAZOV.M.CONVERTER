using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Список движков и подбор по паре «из какого формата в какой».
/// </summary>
internal sealed class EngineRegistry
{
    private readonly IReadOnlyList<IConversionEngine> _engines;

    internal EngineRegistry(IEnumerable<IConversionEngine> engines)
    {
        ArgumentNullException.ThrowIfNull(engines);
        _engines = [.. engines];
    }

    /// <summary>
    /// Все движки, по порядку подбора.
    /// </summary>
    internal IReadOnlyList<IConversionEngine> Engines => _engines;

    /// <summary>
    /// Реестр по умолчанию: звук и изображения через FFmpeg.
    /// </summary>
    internal static EngineRegistry CreateDefault()
    {
        return new EngineRegistry([new FfmpegAudioEngine(), new FfmpegImageEngine()]);
    }

    /// <summary>
    /// Подбирает первый движок, умеющий пару; нет такого — <c>null</c>.
    /// </summary>
    /// <param name="from">Формат входа.</param>
    /// <param name="to">Формат результата.</param>
    internal IConversionEngine? Find(FileFormat from, FileFormat to)
    {
        return _engines.FirstOrDefault(engine => engine.CanConvert(from, to));
    }

    /// <summary>
    /// Все пары всех движков с именем нужной программы — для каталога возможностей; порядок стабильный.
    /// </summary>
    internal IReadOnlyList<(FileFormat From, FileFormat To, string ToolName)> Pairs()
    {
        return
        [
            .. _engines.SelectMany(engine => engine.Pairs.Select(pair => (pair.From, pair.To, engine.ToolName))),
        ];
    }
}
