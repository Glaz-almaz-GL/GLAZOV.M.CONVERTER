namespace GLAZOV.M.CONVERTER.Execution;

/// <summary>
/// Итог одной задачи очереди.
/// </summary>
/// <typeparam name="T">Тип значения результата.</typeparam>
/// <param name="Name">Имя задачи.</param>
/// <param name="Outcome">Выполнена, не удалась или отменена.</param>
/// <param name="Value">Значение, если выполнена.</param>
/// <param name="Error">Исключение, если не удалась.</param>
internal sealed record WorkResult<T>(string Name, WorkOutcome Outcome, T? Value, Exception? Error);
