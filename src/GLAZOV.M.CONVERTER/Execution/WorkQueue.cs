namespace GLAZOV.M.CONVERTER.Execution;

/// <summary>
/// Очередь задач с лимитом одновременного выполнения. О форматах и конвертации ничего не знает.
/// Жёсткого потолка нет: число одновременных задач — аргумент вызывающего (настройка библиотеки).
/// </summary>
internal static class WorkQueue
{
    /// <summary>
    /// Выполняет задачи, не более <paramref name="maxParallel"/> одновременно; результаты — в порядке входов.
    /// Неудача задачи не останавливает остальные. Отмена токеном прекращает запуск новых:
    /// невыполненные получают <see cref="WorkOutcome.Canceled"/>.
    /// </summary>
    /// <typeparam name="T">Тип значения результата задачи.</typeparam>
    /// <param name="items">Задачи.</param>
    /// <param name="maxParallel">Сколько задач выполняется одновременно, не меньше 1.</param>
    /// <param name="progress">Приёмник хода: после завершения каждой задачи (в том числе отменённой).</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal static async Task<IReadOnlyList<WorkResult<T>>> RunAsync<T>(
        IReadOnlyList<WorkItem<T>> items,
        int maxParallel,
        IProgress<(int Completed, int Total, string Current)>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxParallel, 1);

        var results = new WorkResult<T>[items.Count];
        var next = -1;
        var completed = 0;

        async Task WorkerAsync()
        {
            while (true)
            {
                var index = Interlocked.Increment(ref next);
                if (index >= items.Count)
                {
                    return;
                }

                results[index] = await RunOneAsync(items[index], cancellationToken).ConfigureAwait(false);
                progress?.Report((Interlocked.Increment(ref completed), items.Count, items[index].Name));
            }
        }

        var workers = Enumerable.Range(0, Math.Min(maxParallel, items.Count)).Select(_ => Task.Run(WorkerAsync, CancellationToken.None));
        await Task.WhenAll(workers).ConfigureAwait(false);
        return results;
    }

    private static async Task<WorkResult<T>> RunOneAsync<T>(WorkItem<T> item, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new WorkResult<T>(item.Name, WorkOutcome.Canceled, default, null);
        }

        try
        {
            var value = await item.Run(cancellationToken).ConfigureAwait(false);
            return new WorkResult<T>(item.Name, WorkOutcome.Completed, value, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new WorkResult<T>(item.Name, WorkOutcome.Canceled, default, null);
        }
        catch (Exception ex)
        {
            return new WorkResult<T>(item.Name, WorkOutcome.Failed, default, ex);
        }
    }
}

/// <summary>
/// Одна задача очереди: имя для отчёта и действие.
/// </summary>
/// <typeparam name="T">Тип значения результата.</typeparam>
/// <param name="Name">Имя задачи (имя входа).</param>
/// <param name="Run">Действие; получает токен отмены очереди.</param>
internal sealed record WorkItem<T>(string Name, Func<CancellationToken, Task<T>> Run);

/// <summary>
/// Итог одной задачи очереди.
/// </summary>
/// <typeparam name="T">Тип значения результата.</typeparam>
/// <param name="Name">Имя задачи.</param>
/// <param name="Outcome">Выполнена, не удалась или отменена.</param>
/// <param name="Value">Значение, если выполнена.</param>
/// <param name="Error">Исключение, если не удалась.</param>
internal sealed record WorkResult<T>(string Name, WorkOutcome Outcome, T? Value, Exception? Error);

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
