using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.Batch;

/// <summary>
/// Итог по одному входу пакета.
/// </summary>
public sealed class BatchItemResult
{
    internal BatchItemResult(
        string sourceName,
        bool succeeded,
        bool canceled,
        ConversionStage? stage,
        ConversionFailureReason? reason,
        string? message,
        string? outputPath)
    {
        SourceName = sourceName;
        Succeeded = succeeded;
        Canceled = canceled;
        Stage = stage;
        Reason = reason;
        Message = message;
        OutputPath = outputPath;
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
    /// Куда записан результат, если он записан в файл.
    /// </summary>
    public string? OutputPath { get; }
}
