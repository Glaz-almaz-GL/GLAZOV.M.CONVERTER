using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Список движков и подбор по паре «из какого формата в какой».
/// </summary>
internal sealed class EngineRegistry
{
    internal EngineRegistry(IEnumerable<IConversionEngine> engines)
    {
        ArgumentNullException.ThrowIfNull(engines);
        Engines = [.. engines];
    }

    /// <summary>
    /// Все движки, по порядку подбора.
    /// </summary>
    internal IReadOnlyList<IConversionEngine> Engines { get; }

    /// <summary>
    /// Реестр по умолчанию: встроенные движки; у ядра их нет, движки приносят расширения.
    /// </summary>
    internal static EngineRegistry CreateDefault()
    {
        return new EngineRegistry([]);
    }

    /// <summary>
    /// Подбирает первый движок, умеющий пару; нет такого — <c>null</c>.
    /// </summary>
    /// <param name="from">Формат входа.</param>
    /// <param name="to">Формат результата.</param>
    internal IConversionEngine? Find(FileFormat from, FileFormat to)
    {
        ConversionPair pair = new(from, to);
        return Engines.FirstOrDefault(engine => engine.Pairs.Contains(pair));
    }

    /// <summary>
    /// Пары всех движков с описаниями программ движка (пусто — программа не нужна) — для каталога возможностей; порядок стабильный,
    /// пара, заявленная несколькими движками, встречается один раз — с тем движком, который подключён раньше (он и победит при подборе).
    /// </summary>
    internal IReadOnlyList<(FileFormat From, FileFormat To, IReadOnlyList<ToolSpec> Tools)> Pairs()
    {
        List<(FileFormat From, FileFormat To, IReadOnlyList<ToolSpec> Tools)> pairs = [];
        HashSet<ConversionPair> seen = [];

        foreach (IConversionEngine engine in Engines)
        {
            pairs.AddRange(engine.Pairs.Where(seen.Add).Select(pair => (pair.From, pair.To, engine.Tools)));
        }

        return pairs;
    }
}
