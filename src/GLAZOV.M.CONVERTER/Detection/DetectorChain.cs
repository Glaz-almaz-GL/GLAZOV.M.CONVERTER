using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Ряд определителей формата: метки всех расширений в порядке подключения, затем второй взгляд на контейнер
/// (<see cref="IStreamFormatDetector"/>, только на входе с перемоткой и только если метки ничего не назвали), затем определители
/// по содержимому (<see cref="IFormatDetector.ByContent"/>); побеждает первый, кто назвал формат; никто — <see cref="FileFormat.Unknown"/>.
/// Один и тот же экземпляр определителя учитывается один раз; определители по содержимому спрашиваются в порядке подключения, поэтому тот, что берёт
/// почти всё (текст), подключается последним.
/// Начало входа — окно определения, но не короче, чем просят определители.
/// </summary>
internal sealed class DetectorChain
{
    /// <summary>
    /// Budget of bytes the second look of one detector may read in all. A fixed value, not a setting: the structures of a real document are far smaller,
    /// and a larger value would only let a forged header make the library read and keep more.
    /// </summary>
    internal const long DefaultInspectBudget = 16L * 1024 * 1024;

    private readonly IReadOnlyList<IFormatDetector> _labels;
    private readonly IReadOnlyList<IStreamFormatDetector> _inspectors;
    private readonly IReadOnlyList<IFormatDetector> _contents;
    private readonly long _inspectBudget;

    internal DetectorChain(
        IEnumerable<IFormatDetector> detectors,
        int window = ConverterSettings.DefaultDetectionWindow,
        long inspectBudget = DefaultInspectBudget)
    {
        ArgumentNullException.ThrowIfNull(detectors);
        HashSet<IFormatDetector> seen = new(ReferenceEqualityComparer.Instance);
        List<IFormatDetector> all = [.. detectors.Where(seen.Add)];

        _labels = [.. all.Where(d => !d.ByContent)];
        _inspectors = [.. all.OfType<IStreamFormatDetector>()];
        _contents = [.. all.Where(d => d.ByContent)];
        _inspectBudget = inspectBudget;
        HeadLength = Math.Max(window, all.Select(d => d.HeadLength).DefaultIfEmpty(0).Max());
    }

    /// <summary>
    /// Сколько начальных байтов читать, чтобы хватило каждому определителю.
    /// </summary>
    internal int HeadLength { get; }

    /// <summary>
    /// Определяет формат по началу данных: метки, затем определители по содержимому (второй взгляд не работает без входа).
    /// </summary>
    /// <param name="head">Начальные байты входа.</param>
    internal FileFormat Detect(ReadOnlySpan<byte> head)
    {
        return MatchLabels(head) ?? MatchContent(head) ?? FileFormat.Unknown;
    }

    /// <summary>
    /// Определяет формат по входу: метки, второй взгляд, определители по содержимому.
    /// </summary>
    /// <param name="head">Начальные байты входа.</param>
    /// <param name="handle">Открытый вход; без перемотки второй взгляд пропускается.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal async Task<FileFormat> DetectAsync(ReadOnlyMemory<byte> head, InputHandle handle, CancellationToken cancellationToken)
    {
        FileFormat? found = MatchLabels(head.Span);
        if (found is null && handle.CanProbe && _inspectors.Any(d => d.CanInspect(head.Span)))
        {
            found = await Task.Run(() => Inspect(head.Span.ToArray(), handle), cancellationToken).ConfigureAwait(false);
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

    // Each inspector reads on its own view of the input, at most the budget in all; what it cannot read it does not claim.
    private FileFormat? Inspect(byte[] head, InputHandle handle)
    {
        foreach (IStreamFormatDetector detector in _inspectors)
        {
            if (!detector.CanInspect(head))
            {
                continue;
            }

            FileFormat? found = handle.Probe(_inspectBudget, stream => Ask(detector, stream));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static FileFormat? Ask(IStreamFormatDetector detector, Stream stream)
    {
        try
        {
            return detector.Detect(stream);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return null;
        }
    }
}
