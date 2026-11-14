using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Outcome of a single conversion operation.
/// </summary>
public sealed class ConversionResult
{
    internal ConversionResult(
        bool succeeded,
        FileFormat sourceType,
        FileFormat targetFormat,
        TimeSpan duration)
    {
        Succeeded = succeeded;
        SourceType = sourceType;
        TargetFormat = targetFormat;
        Duration = duration;
    }

    /// <summary>
    /// Gets whether the conversion succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the confirmed input format detected from leading bytes.
    /// </summary>
    public FileFormat SourceType { get; }

    /// <summary>
    /// Gets the target output format.
    /// </summary>
    public FileFormat TargetFormat { get; }

    /// <summary>
    /// Gets the elapsed time of the conversion.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the primary output file path when written to disk; otherwise <c>null</c>.
    /// </summary>
    public string? OutputPath { get; internal init; }

    /// <summary>
    /// Gets every file written by the conversion: one for a file target, several when a pair writes multiple files to a folder,
    /// or empty for stream and memory targets.
    /// </summary>
    public IReadOnlyList<string> OutputPaths { get; internal init; } = [];

    /// <summary>
    /// Gets diagnostic notes added by the engine (such as the selected spreadsheet sheet); empty when none were recorded.
    /// </summary>
    public IReadOnlyList<string> Notes { get; internal init; } = [];

    /// <summary>
    /// Gets the output bytes when targeting memory; otherwise <c>null</c>.
    /// </summary>
    public byte[]? Bytes { get; internal init; }

    /// <summary>
    /// Gets a readable stream over the output when targeting memory; otherwise <c>null</c>.
    /// </summary>
    public Stream? Stream { get; internal init; }

    /// <summary>
    /// Gets whether at least one track was re-encoded; <c>false</c> only when all tracks were stream-copied without re-encoding.
    /// </summary>
    public bool Reencoded { get; internal init; } = true;
}