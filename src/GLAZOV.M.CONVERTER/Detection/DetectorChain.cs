using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Цепочка определителей формата: опрашиваются по порядку, побеждает первый, кто назвал формат; никто — <see cref="FileFormat.Unknown"/>.
/// Окно чтения — наибольшее из запрошенных.
/// </summary>
internal sealed class DetectorChain
{
    private readonly IReadOnlyList<IFormatDetector> _detectors;

    internal DetectorChain(IEnumerable<IFormatDetector> detectors)
    {
        ArgumentNullException.ThrowIfNull(detectors);
        _detectors = [.. detectors];
        HeadLength = _detectors.Count == 0 ? 0 : _detectors.Max(d => d.HeadLength);
    }

    /// <summary>
    /// Цепочка из одних готовых признаков — для публичного <see cref="FileTypeDetector"/>, не знающего о расширениях конвертера.
    /// </summary>
    internal static DetectorChain BuiltIn { get; } = new([new SignatureDetector()]);

    /// <summary>
    /// Сколько начальных байтов читать, чтобы хватило каждому определителю.
    /// </summary>
    internal int HeadLength { get; }

    /// <summary>
    /// Определяет формат по началу данных.
    /// </summary>
    /// <param name="head">Начальные байты входа.</param>
    internal FileFormat Detect(ReadOnlySpan<byte> head)
    {
        foreach (IFormatDetector detector in _detectors)
        {
            FileFormat? found = detector.Detect(head);
            if (found is not null)
            {
                return found;
            }
        }

        return FileFormat.Unknown;
    }
}
