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
    /// Вход выполнен: результат записан (путь — если в файл).
    /// </summary>
    /// <param name="sourceName">Имя входа.</param>
    /// <param name="outputPath">Куда записан результат; пусто, если не в файл.</param>
    internal static BatchItemResult Success(string sourceName, string? outputPath)
    {
        return new BatchItemResult(sourceName, true, false, null, null, null, outputPath);
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
    /// Куда записан результат, если он записан в файл.
    /// </summary>
    public string? OutputPath { get; }
}
