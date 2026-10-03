using GLAZOV.M.CONVERTER.Execution;

namespace GLAZOV.M.CONVERTER.Tests.Execution;

/// <summary>
/// Очередь с лимитом: пик одновременных задач доказывается счётчиком, а не временем — задачи ждут, пока не наберётся нужное число.
/// </summary>
public class WorkQueueTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private sealed class Peak
    {
        private int _running;
        private int _peak;

        public int Value => Volatile.Read(ref _peak);

        public int Enter()
        {
            var now = Interlocked.Increment(ref _running);
            int seen;
            while (now > (seen = Volatile.Read(ref _peak)))
            {
                Interlocked.CompareExchange(ref _peak, now, seen);
            }

            return now;
        }

        public void Leave() => Interlocked.Decrement(ref _running);
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly List<T> _items = [];

        public IReadOnlyList<T> Items
        {
            get
            {
                lock (_items)
                {
                    return _items.ToList();
                }
            }
        }

        public void Report(T value)
        {
            lock (_items)
            {
                _items.Add(value);
            }
        }
    }

    // Задача, которая не завершается, пока одновременно не работает target задач (или не кончится терпение).
    private static WorkItem<int> Gathering(string name, Peak peak, int target, TaskCompletionSource gate) =>
        new(name, async ct =>
        {
            if (peak.Enter() >= target)
            {
                gate.TrySetResult();
            }

            try
            {
                await gate.Task.WaitAsync(Patience, ct);
                return 1;
            }
            finally
            {
                peak.Leave();
            }
        });

    [Fact]
    public async Task Limit4_On50Items_PeakIsExactlyFour()
    {
        var peak = new Peak();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var items = Enumerable.Range(0, 50).Select(i => Gathering($"f{i}", peak, 4, gate)).ToList();

        var results = await WorkQueue.RunAsync(items, 4, null, CancellationToken.None);

        Assert.Equal(4, peak.Value);
        Assert.All(results, r => Assert.Equal(WorkOutcome.Completed, r.Outcome));
    }

    [Fact]
    public async Task Limit64_On50Items_NoCeilingAtFour()
    {
        var peak = new Peak();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var items = Enumerable.Range(0, 50).Select(i => Gathering($"f{i}", peak, 50, gate)).ToList();

        var results = await WorkQueue.RunAsync(items, 64, null, CancellationToken.None);

        Assert.Equal(50, peak.Value);
        Assert.Equal(50, results.Count(r => r.Outcome == WorkOutcome.Completed));
    }

    [Fact]
    public async Task ResultsKeepInputOrder_AndCarryValues()
    {
        var items = Enumerable.Range(0, 20)
            .Select(i => new WorkItem<int>($"n{i}", async _ =>
            {
                await Task.Delay((20 - i) % 5);
                return i * 10;
            }))
            .ToList();

        var results = await WorkQueue.RunAsync(items, 6, null, CancellationToken.None);

        Assert.Equal(Enumerable.Range(0, 20).Select(i => i * 10), results.Select(r => r.Value));
        Assert.Equal(Enumerable.Range(0, 20).Select(i => $"n{i}"), results.Select(r => r.Name));
    }

    [Fact]
    public async Task OneFailure_DoesNotStopTheOthers_AndKeepsItsError()
    {
        var items = Enumerable.Range(0, 10)
            .Select(i => new WorkItem<int>($"n{i}", _ => i == 3
                ? throw new InvalidOperationException("сломалось")
                : Task.FromResult(i)))
            .ToList();

        var results = await WorkQueue.RunAsync(items, 3, null, CancellationToken.None);

        Assert.Equal(9, results.Count(r => r.Outcome == WorkOutcome.Completed));
        var failed = Assert.Single(results, r => r.Outcome == WorkOutcome.Failed);
        Assert.Equal("n3", failed.Name);
        Assert.IsType<InvalidOperationException>(failed.Error);
    }

    [Fact]
    public async Task TaskThrowingCancellationWithoutTokenCancelled_IsFailureNotCancel()
    {
        var items = new[] { new WorkItem<int>("odd", _ => throw new OperationCanceledException()) };

        var results = await WorkQueue.RunAsync(items, 1, null, CancellationToken.None);

        Assert.Equal(WorkOutcome.Failed, results[0].Outcome);
    }

    [Fact]
    public async Task Cancel_StopsLaunchingNew_RunningAreCanceled_RestAreCanceledNotRun()
    {
        using var cts = new CancellationTokenSource();
        var started = 0;
        var startedTwo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var items = Enumerable.Range(0, 10)
            .Select(i => new WorkItem<int>($"n{i}", async ct =>
            {
                if (Interlocked.Increment(ref started) == 2)
                {
                    startedTwo.SetResult();
                }

                await Task.Delay(Patience, ct);
                return i;
            }))
            .ToList();

        var run = WorkQueue.RunAsync(items, 2, null, cts.Token);
        await startedTwo.Task.WaitAsync(Patience);
        await cts.CancelAsync();
        var results = await run;

        Assert.Equal(2, Volatile.Read(ref started));
        Assert.All(results, r => Assert.Equal(WorkOutcome.Canceled, r.Outcome));
    }

    [Fact]
    public async Task CancelledBeforeStart_NothingRuns()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var ran = 0;
        var items = Enumerable.Range(0, 5)
            .Select(i => new WorkItem<int>($"n{i}", _ =>
            {
                Interlocked.Increment(ref ran);
                return Task.FromResult(i);
            }))
            .ToList();

        var results = await WorkQueue.RunAsync(items, 3, null, cts.Token);

        Assert.Equal(0, ran);
        Assert.All(results, r => Assert.Equal(WorkOutcome.Canceled, r.Outcome));
    }

    [Fact]
    public async Task Progress_ReportsOncePerItem_EndsAtTotal()
    {
        var progress = new SyncProgress<(int Completed, int Total, string Current)>();
        var items = Enumerable.Range(0, 12).Select(i => new WorkItem<int>($"n{i}", _ => Task.FromResult(i))).ToList();

        await WorkQueue.RunAsync(items, 4, progress, CancellationToken.None);

        var reports = progress.Items;
        Assert.Equal(12, reports.Count);
        Assert.All(reports, r => Assert.Equal(12, r.Total));
        Assert.Equal(Enumerable.Range(1, 12), reports.Select(r => r.Completed).Order());
        Assert.Equal(12, reports.Max(r => r.Completed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task LimitBelowOne_IsAUsageError(int limit)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await WorkQueue.RunAsync(new List<WorkItem<int>>(), limit, null, CancellationToken.None));
    }

    [Fact]
    public async Task NoItems_NoResults()
    {
        var results = await WorkQueue.RunAsync(new List<WorkItem<int>>(), 4, null, CancellationToken.None);

        Assert.Empty(results);
    }
}
