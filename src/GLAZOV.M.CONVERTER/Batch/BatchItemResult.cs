using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.Batch;

/// <summary>
/// Итог по одному входу пакета.
/// </summary>
public sealed class BatchItemResult
{
    private BatchItemResult(
        string sourceName,
        bool succeeded,
        bool canceled,
        ConversionStage? stage,
        ConversionFailureReason? reason,
        string? message,
        string? outputPath,
        IReadOnlyList<string>? outputPaths = null,
        IReadOnlyList<string>? notes = null)
    {
        OutputPaths = outputPaths ?? (outputPath is null ? [] : [outputPath]);
        Notes = notes ?? [];
        SourceName = sourceName;
        Succeeded = succeeded;
        Canceled = canceled;
        Stage = stage;
        Reason = reason;
        Message = message;
        OutputPath = outputPath;
    }

    /// <summary>
    /// Вход выполнен: результат записан (путь — если в файл).
    /// </summary>
    /// <param name="sourceName">Имя входа.</param>
    /// <param name="outputPath">Куда записан результат; пусто, если не в файл.</param>
    /// <param name="outputPaths">Every file written for this input.</param>
    /// <param name="notes">Lines the engine added about this input.</param>
    internal static BatchItemResult Success(string sourceName, string? outputPath, IReadOnlyList<string>? outputPaths = null, IReadOnlyList<string>? notes = null)
    {
        return new BatchItemResult(sourceName, true, false, null, null, null, outputPath, outputPaths, notes);
    }

    /// <summary>
    /// Вход не удался: этап, причина и сообщение берутся из ошибки конвертации.
    /// </summary>
    /// <param name="sourceName">Имя входа.</param>
    /// <param name="error">Ошибка конвертации.</param>
    internal static BatchItemResult Failure(string sourceName, ConversionException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BatchItemResult(sourceName, false, false, error.Stage, error.Reason, error.Message, null);
    }

    /// <summary>
    /// Вход отменён: не запускался или прерван отменой.
    /// </summary>
    /// <param name="sourceName">Имя входа.</param>
    internal static BatchItemResult Cancelled(string sourceName)
    {
        return new BatchItemResult(sourceName, false, true, null, null, null, null);
    }

    /// <summary>
    /// Имя входа.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// Удалась ли конвертация этого входа.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Отменён ли этот вход.
    /// </summary>
    public bool Canceled { get; }

    /// <summary>
    /// Этап ошибки; пусто, если ошибки не было.
    /// </summary>
    public ConversionStage? Stage { get; }

    /// <summary>
    /// Причина ошибки; пусто, если ошибки не было.
    /// </summary>
    public ConversionFailureReason? Reason { get; }

    /// <summary>
    /// Понятное сообщение об ошибке; пусто, если ошибки не было.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Куда записан результат, если он записан в файл; при нескольких файлах — первый из <see cref="OutputPaths"/>.
    /// </summary>
    public string? OutputPath { get; }

    /// <summary>
    /// Every file written for this input; empty when it failed or was canceled.
    /// </summary>
    public IReadOnlyList<string> OutputPaths { get; }

    /// <summary>
    /// Lines the engine added about this input; empty when it failed or was canceled.
    /// </summary>
    public IReadOnlyList<string> Notes { get; }
}
