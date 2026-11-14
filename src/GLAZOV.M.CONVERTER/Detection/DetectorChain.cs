using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Ordered pipeline of format detectors: fast header signatures first, followed by deep stream container inspectors
/// (on seekable inputs only), and finally heuristic content detectors.
/// </summary>
internal sealed class DetectorChain
{
    /// <summary>
    /// Maximum number of bytes a stream detector may read during structural inspection.
    /// </summary>
    internal const long DefaultInspectBudget = 16L * 1024 * 1024;

    /// <summary>
    /// Number of leading bytes read to satisfy every registered detector.
    /// </summary>
    internal int HeadLength { get; }

    private readonly IFormatDetector[] _labels;
    private readonly IStreamFormatDetector[] _inspectors;
    private readonly IFormatDetector[] _contents;
    private readonly long _inspectBudget;

    internal DetectorChain(
        IEnumerable<IFormatDetector> detectors,
        int window = ConverterSettings.DefaultDetectionWindow,
        long inspectBudget = DefaultInspectBudget)
    {
        ArgumentNullException.ThrowIfNull(detectors);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(window);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inspectBudget);

        HashSet<IFormatDetector> seen = new(ReferenceEqualityComparer.Instance);
        List<IFormatDetector> all = [.. detectors.Where(seen.Add)];

        _labels = [.. all.Where(d => !d.ByContent)];
        _inspectors = [.. all.OfType<IStreamFormatDetector>()];
        _contents = [.. all.Where(d => d.ByContent)];
        _inspectBudget = inspectBudget;
        HeadLength = Math.Max(window, all.Select(d => d.HeadLength).DefaultIfEmpty(0).Max());
    }

    /// <summary>
    /// Synchronously detects the format using only the leading byte buffer (header and content detectors).
    /// </summary>
    internal FileFormat Detect(ReadOnlySpan<byte> head)
    {
        return MatchLabels(head) ?? MatchContent(head) ?? FileFormat.Unknown;
    }

    /// <summary>
    /// Detects the format asynchronously using leading bytes and, when supported, deep stream inspection.
    /// </summary>
    internal async ValueTask<FileFormat> DetectAsync(ReadOnlyMemory<byte> head, InputHandle handle, CancellationToken cancellationToken)
    {
        FileFormat? found = MatchLabels(head.Span);
        if (found is null && handle.CanProbe && HasMatchingInspector(head.Span))
        {
            found = await InspectAsync(head, handle, cancellationToken).ConfigureAwait(false);
        }

        return found ?? MatchContent(head.Span) ?? FileFormat.Unknown;
    }

    private FileFormat? MatchLabels(ReadOnlySpan<byte> head)
    {
        foreach (IFormatDetector detector in _labels)
        {
            FileFormat? found = detector.Detect(head);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private FileFormat? MatchContent(ReadOnlySpan<byte> head)
    {
        foreach (IFormatDetector detector in _contents)
        {
            FileFormat? found = detector.Detect(head);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private bool HasMatchingInspector(ReadOnlySpan<byte> head)
    {
        foreach (IStreamFormatDetector detector in _inspectors)
        {
            if (detector.CanInspect(head))
            {
                return true;
            }
        }

        return false;
    }

    private async ValueTask<FileFormat?> InspectAsync(ReadOnlyMemory<byte> head, InputHandle handle, CancellationToken cancellationToken)
    {
        foreach (IStreamFormatDetector detector in _inspectors)
        {
            if (!detector.CanInspect(head.Span))
            {
                continue;
            }

            FileFormat? found = await handle.ProbeAsync(
                _inspectBudget,
                (stream, ct) => AskAsync(detector, stream, ct),
                cancellationToken).ConfigureAwait(false);

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static async ValueTask<FileFormat?> AskAsync(IStreamFormatDetector detector, Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await detector.DetectAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return null;
        }
    }
}
