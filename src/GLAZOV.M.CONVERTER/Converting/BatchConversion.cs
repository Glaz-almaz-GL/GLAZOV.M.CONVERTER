using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Executes batch conversions over a folder or a list of sources with bounded parallelism.
/// Individual item conversion failures are captured in <see cref="BatchReport"/>; unexpected system failures are rethrown.
/// </summary>
internal sealed class BatchConversion(ConverterSettingsSnapshot settings, SingleConversion single)
{
    /// <summary>
    /// Converts matching files inside a folder source into the target folder.
    /// </summary>
    internal Task<BatchReport> RunFolderAsync(
        Source folder,
        Target target,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (folder.Kind != SourceKind.Folder)
        {
            // Source is not folder
            throw new ArgumentException("Expected a folder source (Source.Folder); pass files or streams as a list.", nameof(folder));
        }

        return RunAsync(InputReader.Expand(folder), target, progress, cancellationToken);
    }

    /// <summary>
    /// Converts a collection of file or stream sources into the target folder.
    /// </summary>
    internal Task<BatchReport> RunListAsync(
        IEnumerable<Source> sources,
        Target target,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        Source[] list = [.. sources];
        if (list.Length == 0)
        {
            throw new ArgumentException("The source list is empty: nothing to convert.", nameof(sources));
        }

        if (list.Any(s => s is null || s.Kind == SourceKind.Folder))
        {
            // Folders not allowed
            throw new ArgumentException("Only file and stream sources are allowed in a list; convert a folder with a separate call.", nameof(sources));
        }

        return RunAsync(list, target, progress, cancellationToken);
    }

    private async Task<BatchReport> RunAsync(
        IReadOnlyList<Source> sources,
        Target target,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (target.Kind != TargetKind.Folder)
        {
            throw new ArgumentException("Batch conversion requires a folder target (Target.Folder).", nameof(target));
        }

        cancellationToken.ThrowIfCancellationRequested();

        PlannedItem[] plan = BuildPlan(sources, target.FileFormat.Extension);
        BatchItemResult[] results = new BatchItemResult[plan.Length];
        BatchState state = new(plan, results, target, progress, cancellationToken);

        int workerCount = Math.Min(settings.MaxParallelTasks, plan.Length);
        await Task.WhenAll(Enumerable.Range(0, workerCount).Select(_ => Task.Run(() => RunWorkerAsync(state), CancellationToken.None))).ConfigureAwait(false);

        return new BatchReport(results, sources);
    }

    private static PlannedItem[] BuildPlan(IReadOnlyList<Source> sources, string targetExtension)
    {
        HashSet<string> takenNames = new(StringComparer.OrdinalIgnoreCase);
        PlannedItem[] plan = new PlannedItem[sources.Count];

        for (int i = 0; i < sources.Count; i++)
        {
            string name = InputReader.NameOf(sources[i]);
            string outputName = $"{Path.GetFileNameWithoutExtension(name)}.{targetExtension}";
            string? duplicateName = takenNames.Add(outputName) ? null : outputName;
            plan[i] = new PlannedItem(sources[i], name, duplicateName);
        }

        return plan;
    }

    private async Task RunWorkerAsync(BatchState state)
    {
        while (true)
        {
            int index = state.ClaimNextIndex();
            if (index >= state.Plan.Length)
            {
                return;
            }

            PlannedItem item = state.Plan[index];
            state.Results[index] = await ExecuteItemAsync(item, state.Target, state.CancellationToken).ConfigureAwait(false);
            state.ReportItemCompleted(item.Name);
        }
    }

    private async Task<BatchItemResult> ExecuteItemAsync(
        PlannedItem item,
        Target target,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return BatchItemResult.Cancelled(item.Name);
        }

        if (item.DuplicateOutputName is not null)
        {
            return BatchItemResult.Failure(item.Name, Failures.DuplicateOutput(item.Name, item.DuplicateOutputName));
        }

        try
        {
            ConversionResult result = await single.RunAsync(item.Source, target, null, null, cancellationToken).ConfigureAwait(false);
            return BatchItemResult.Success(item.Name, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BatchItemResult.Cancelled(item.Name);
        }
        catch (ConversionException ex)
        {
            return BatchItemResult.Failure(item.Name, ex);
        }
    }

    private readonly record struct PlannedItem(Source Source, string Name, string? DuplicateOutputName);

    private sealed class BatchState(
        PlannedItem[] plan,
        BatchItemResult[] results,
        Target target,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        private int _nextIndex = -1;
        private int _completedCount;

        internal PlannedItem[] Plan => plan;

        internal BatchItemResult[] Results => results;

        internal Target Target => target;

        internal CancellationToken CancellationToken => cancellationToken;

        internal int ClaimNextIndex()
        {
            return Interlocked.Increment(ref _nextIndex);
        }

        internal void ReportItemCompleted(string currentName)
        {
            progress?.Report(new BatchProgress(Interlocked.Increment(ref _completedCount), plan.Length, currentName));
        }
    }
}
