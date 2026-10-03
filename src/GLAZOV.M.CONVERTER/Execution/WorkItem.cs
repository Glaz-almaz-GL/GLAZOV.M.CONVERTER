namespace GLAZOV.M.CONVERTER.Execution;

/// <summary>
/// Одна задача очереди: имя для отчёта и действие.
/// </summary>
/// <typeparam name="T">Тип значения результата.</typeparam>
/// <param name="Name">Имя задачи (имя входа).</param>
/// <param name="Run">Действие; получает токен отмены очереди.</param>
internal sealed record WorkItem<T>(string Name, Func<CancellationToken, Task<T>> Run);
