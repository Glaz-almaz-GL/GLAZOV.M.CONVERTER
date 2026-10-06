using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tests.Extension;

/// <summary>
/// Builds the extension that makes the core recognise the stand-in formats of <see cref="TestFormats"/>.
/// </summary>
public static class TestExtensions
{
    /// <summary>
    /// An extension with one header detector per stand-in format and the given engines.
    /// </summary>
    public static ConfigurableExtension Create(params IConversionEngine[] engines)
    {
        IFormatDetector[] detectors = [.. TestFormats.All.Select(format => new PatternDetector(TestFormats.Header(format), format, 16))];
        return new ConfigurableExtension("test-formats", detectors, engines)
        {
            Declared = TestFormats.All,
        };
    }
}
