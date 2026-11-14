using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// Test stream detector: inspects the input stream when its leading bytes match the given pattern
/// and returns the result of the provided inspection delegate.
/// </summary>
public sealed class InspectingDetector(byte[] pattern, Func<Stream, FileFormat?> inspect) : IStreamFormatDetector
{
    /// <inheritdoc />
    public int HeadLength => pattern.Length;

    /// <summary>
    /// Number of times <see cref="Detect(ReadOnlySpan{byte})"/> was invoked.
    /// </summary>
    public int HeadCalls { get; private set; }

    /// <summary>
    /// Number of times <see cref="DetectAsync"/> was invoked.
    /// </summary>
    public int InspectCalls { get; private set; }

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        HeadCalls++;
        return null;
    }

    /// <inheritdoc />
    public bool CanInspect(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(pattern);
    }

    /// <inheritdoc />
    public ValueTask<FileFormat?> DetectAsync(Stream input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InspectCalls++;
        return ValueTask.FromResult(inspect(input));
    }
}
