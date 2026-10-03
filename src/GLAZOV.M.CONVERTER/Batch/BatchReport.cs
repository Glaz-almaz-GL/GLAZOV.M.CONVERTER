using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Batch;

/// <summary>
/// Отчёт по пакету: что получилось, что нет и почему.
/// </summary>
public sealed class BatchReport
{
    private readonly IReadOnlyList<Source> _sources;

    internal BatchReport(IReadOnlyList<BatchItemResult> items, IReadOnlyList<Source> sources)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sources);
        if (items.Count != sources.Count)
        {
            throw new ArgumentException("Записей отчёта и входов должно быть поровну: отчёт повторяет входы по порядку.", nameof(sources));
        }

        Items = items;
        _sources = sources;
    }

    /// <summary>
    /// Сколько входов было всего.
    /// </summary>
    public int Total => Items.Count;

    /// <summary>
    /// Сколько удалось.
    /// </summary>
    public int Succeeded => Items.Count(i => i.Succeeded);

    /// <summary>
    /// Сколько завершилось ошибкой.
    /// </summary>
    public int Failed => Items.Count(i => !i.Succeeded && !i.Canceled);

    /// <summary>
    /// Сколько отменено.
    /// </summary>
    public int Canceled => Items.Count(i => i.Canceled);

    /// <summary>
    /// Итог по каждому входу.
    /// </summary>
    public IReadOnlyList<BatchItemResult> Items { get; }

    /// <summary>
    /// Входы, завершившиеся ошибкой, — чтобы повторить только их.
    /// </summary>
    public IReadOnlyList<Source> FailedSources()
    {
        return
        [
            .. Items.Select((item, index) => (item, index))
                .Where(pair => !pair.item.Succeeded && !pair.item.Canceled)
                .Select(pair => _sources[pair.index]),
        ];
    }
}
