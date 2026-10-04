using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

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
    /// Реестр по умолчанию: звук, изображения и видео через FFmpeg.
    /// </summary>
    internal static EngineRegistry CreateDefault()
    {
        return new EngineRegistry(new BuiltInExtension().Engines);
    }

    /// <summary>
    /// Подбирает первый движок, умеющий пару; нет такого — <c>null</c>.
    /// </summary>
    /// <param name="from">Формат входа.</param>
    /// <param name="to">Формат результата.</param>
    internal IConversionEngine? Find(FileFormat from, FileFormat to)
    {
        ConversionPair pair = new(from, to);
        return _engines.FirstOrDefault(engine => engine.Pairs.Contains(pair));
    }

    /// <summary>
    /// Пары всех движков с именем нужной программы (или <c>null</c>) — для каталога возможностей; порядок стабильный,
    /// пара, заявленная несколькими движками, встречается один раз — с тем движком, который подключён раньше (он и победит при подборе).
    /// </summary>
    internal IReadOnlyList<(FileFormat From, FileFormat To, string? ToolName)> Pairs()
    {
        List<(FileFormat From, FileFormat To, string? ToolName)> pairs = [];
        HashSet<ConversionPair> seen = [];
        foreach (IConversionEngine engine in _engines)
        {
            foreach (ConversionPair pair in engine.Pairs)
            {
                if (seen.Add(pair))
                {
                    pairs.Add((pair.From, pair.To, engine.ToolName));
                }
            }
        }

        return pairs;
    }
}
