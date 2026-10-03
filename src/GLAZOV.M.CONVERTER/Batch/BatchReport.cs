namespace GLAZOV.M.CONVERTER;

/// <summary>
/// Отчёт по пакету: что получилось, что нет и почему.
/// </summary>
public sealed class BatchReport
{
    internal BatchReport(IReadOnlyList<BatchItemResult> items) => Items = items;

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
    public IReadOnlyList<Source> FailedSources() => throw Stub.NotYet("BatchReport.FailedSources");
}
