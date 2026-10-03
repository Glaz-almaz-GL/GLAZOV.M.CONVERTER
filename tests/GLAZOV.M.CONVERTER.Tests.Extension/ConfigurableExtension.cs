using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// Расширение, собираемое из переданных определителей и движков, — для проверок подключения, порядка и отказов.
/// </summary>
public sealed class ConfigurableExtension(string name, IReadOnlyList<IFormatDetector> detectors, IReadOnlyList<IConversionEngine> engines) : IConverterExtension
{
    public string Name { get; } = name;

    public IReadOnlyList<IFormatDetector> Detectors { get; } = detectors;

    public IReadOnlyList<IConversionEngine> Engines { get; } = engines;
}
