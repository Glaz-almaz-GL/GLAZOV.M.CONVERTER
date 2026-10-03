namespace GLAZOV.M.CONVERTER.Execution;

/// <summary>
/// Чем кончилась задача очереди.
/// </summary>
internal enum WorkOutcome
{
    /// <summary>Выполнена.</summary>
    Completed,

    /// <summary>Не удалась: исключение сохранено в результате.</summary>
    Failed,

    /// <summary>Отменена: не запускалась или прервана токеном.</summary>
    Canceled,
}
