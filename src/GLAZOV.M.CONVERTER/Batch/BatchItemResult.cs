using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.Batch;

/// <summary>
/// Outcome for a single item in a batch conversion.
/// </summary>
public sealed class BatchItemResult
{
    private BatchItemResult(string sourceName, bool canceled, ConversionResult? result, ConversionException? error)
    {
        SourceName = sourceName;
        Canceled = canceled;
        Result = result;
        Error = error;
    }

    internal static BatchItemResult Success(string sourceName, ConversionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(sourceName, canceled: false, result, error: null);
    }

    internal static BatchItemResult Failure(string sourceName, ConversionException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(sourceName, canceled: false, result: null, error);
    }

    internal static BatchItemResult Cancelled(string sourceName)
    {
        return new(sourceName, canceled: true, result: null, error: null);
    }

    /// <summary>
    /// Display name of the input item.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// Whether this item converted successfully.
    /// </summary>
    public bool Succeeded => Result is not null;

    /// <summary>
    /// Whether this item was skipped or interrupted due to cancellation.
    /// </summary>
    public bool Canceled { get; }

    /// <summary>
    /// Full conversion result when <see cref="Succeeded"/> is <c>true</c>; otherwise <c>null</c>.
    /// </summary>
    public ConversionResult? Result { get; }

    /// <summary>
    /// Conversion error details when the item failed; otherwise <c>null</c>.
    /// </summary>
    public ConversionException? Error { get; }

    /// <summary>
    /// Stage at which the failure occurred; <c>null</c> when succeeded or canceled.
    /// </summary>
    public ConversionStage? Stage => Error?.Stage;

    /// <summary>
    /// Reason for the failure; <c>null</c> when succeeded or canceled.
    /// </summary>
    public ConversionFailureReason? Reason => Error?.Reason;

    /// <summary>
    /// Human-readable error message; <c>null</c> when succeeded or canceled.
    /// </summary>
    public string? Message => Error?.Message;

    /// <summary>
    /// Primary output file path when written to disk; otherwise <c>null</c>.
    /// </summary>
    public string? OutputPath => Result?.OutputPath;

    /// <summary>
    /// Every file written for this item; empty when failed or canceled.
    /// </summary>
    public IReadOnlyList<string> OutputPaths => Result?.OutputPaths ?? [];

    /// <summary>
    /// Diagnostic notes added by the engine; empty when failed or canceled.
    /// </summary>
    public IReadOnlyList<string> Notes => Result?.Notes ?? [];
}
