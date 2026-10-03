using System.Runtime.ExceptionServices;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Execution;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Пакетная конвертация: раскрыть входы, выполнить параллельно (число задач — настройка), собрать отчёт.
/// Неудача отдельного входа в отчёте, а не исключением; исключение — только на неверное использование,
/// на отмену до начала работы и на непредвиденный сбой (его нельзя выдавать за неудачу одного входа).
/// </summary>
internal sealed class BatchConversion
{
    private readonly ConverterSettingsSnapshot _settings;
    private readonly SingleConversion _single;

    internal BatchConversion(ConverterSettingsSnapshot settings, SingleConversion single)
    {
        _settings = settings;
        _single = single;
    }

    /// <summary>
    /// Конвертирует файлы папки (маска и рекурсия — из <see cref="Source.Folder"/>).
    /// </summary>
    /// <param name="folder">Источник-папка.</param>
    /// <param name="target">Целевая папка и формат.</param>
    /// <param name="progress">Приёмник хода.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal Task<BatchReport> RunFolderAsync(
        Source folder,
        Target target,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (folder.Kind != SourceKind.Folder)
        {
            throw new ArgumentException("Ожидалась папка (Source.Folder); для файлов и потоков передайте их списком.", nameof(folder));
        }

        return RunAsync(InputReader.Expand(folder), target, progress, cancellationToken);
    }

    /// <summary>
    /// Конвертирует список входов — файлов и потоков.
    /// </summary>
    /// <param name="sources">Входы; пустой список — неверное использование.</param>
    /// <param name="target">Целевая папка и формат.</param>
    /// <param name="progress">Приёмник хода.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal Task<BatchReport> RunListAsync(
        IEnumerable<Source> sources,
        Target target,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        Source[] list = [.. sources];
        if (list.Length == 0)
        {
            throw new ArgumentException("Список входов пуст: нечего конвертировать.", nameof(sources));
        }

        if (list.Any(s => s is null || s.Kind == SourceKind.Folder))
        {
            throw new ArgumentException("В списке допустимы файлы и потоки; папку передайте отдельным вызовом.", nameof(sources));
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
            throw new ArgumentException("Для пакета нужна целевая папка (Target.Folder).", nameof(target));
        }

        cancellationToken.ThrowIfCancellationRequested();

        HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
        List<WorkItem<ConversionResult>> items = [];
        foreach (Source source in sources)
        {
            string name = InputReader.NameOf(source);
            string outputName = Path.GetFileNameWithoutExtension(name) + "." + target.FileFormat.Extension;
            bool duplicate = !taken.Add(outputName);
            items.Add(new WorkItem<ConversionResult>(name, ct => duplicate
                ? Task.FromException<ConversionResult>(Failures.DuplicateOutput(name, outputName))
                : _single.RunAsync(source, target, null, null, ct)));
        }

        IReadOnlyList<WorkResult<ConversionResult>> results = await WorkQueue.RunAsync(
            items,
            _settings.MaxParallelTasks,
            progress is null ? null : new Adapter(progress),
            cancellationToken).ConfigureAwait(false);

        return new BatchReport([.. results.Select(ToItem)], sources);
    }

    // Непредвиденный сбой (не ошибка конвертации) пакетом не прячется: он выбрасывается как есть.
    private static BatchItemResult ToItem(WorkResult<ConversionResult> result)
    {
        return result.Outcome switch
        {
            WorkOutcome.Completed => BatchItemResult.Success(result.Name, result.Value!.OutputPath),
            WorkOutcome.Canceled => BatchItemResult.Cancelled(result.Name),
            _ when result.Error is ConversionException failure => BatchItemResult.Failure(result.Name, failure),
            _ => throw RethrowUnexpected(result.Error!),
        };
    }

    private static Exception RethrowUnexpected(Exception error)
    {
        ExceptionDispatchInfo.Capture(error).Throw();
        return error;
    }

    private sealed class Adapter(IProgress<BatchProgress> target) : IProgress<(int Completed, int Total, string Current)>
    {
        public void Report((int Completed, int Total, string Current) value)
        {
            target.Report(new BatchProgress(value.Completed, value.Total, value.Current));
        }
    }
}
