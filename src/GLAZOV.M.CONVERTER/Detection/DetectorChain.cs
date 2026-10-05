using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Цепочка определителей формата: опрашиваются по порядку, побеждает первый, кто назвал формат; никто — <see cref="FileFormat.Unknown"/>.
/// Окно чтения — наибольшее из запрошенных. Последним всегда стоит определитель текста: у текста нет своей метки,
/// поэтому он не должен опережать ни готовые признаки, ни признаки расширений.
/// </summary>
internal sealed class DetectorChain
{
    private readonly IReadOnlyList<IFormatDetector> _detectors;

    internal DetectorChain(IEnumerable<IFormatDetector> detectors, long containerProbeLimit = ContainerProbe.DefaultBudget)
    {
        ArgumentNullException.ThrowIfNull(detectors);
        _detectors = [.. detectors, new TextDetector()];
        HeadLength = _detectors.Max(d => d.HeadLength);
        ContainerProbeLimit = containerProbeLimit;
    }

    /// <summary>
    /// Цепочка из одних готовых признаков с окном по умолчанию — для публичного <see cref="FileTypeDetector"/>,
    /// не знающего ни о расширениях, ни о настройках конвертера.
    /// </summary>
    internal static DetectorChain BuiltIn { get; } = new([new SignatureDetector()]);

    /// <summary>
    /// Сколько начальных байтов читать, чтобы хватило каждому определителю.
    /// </summary>
    internal int HeadLength { get; }

    /// <summary>
    /// How many bytes the second look at a container may read in all.
    /// </summary>
    internal long ContainerProbeLimit { get; }

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
