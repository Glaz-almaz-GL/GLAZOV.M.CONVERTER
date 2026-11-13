using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Office.Tests;

/// <summary>
/// Creates a converter with the Office extension plugged in, the way a user of the package does.
/// </summary>
internal static class OfficeConverter
{
    /// <summary>
    /// A converter with <see cref="OfficeExtension"/>; <paramref name="settings"/> and <paramref name="office"/> are used when given.
    /// </summary>
    internal static Converter Create(ConverterSettings? settings = null, OfficeSettings? office = null)
    {
        settings ??= new ConverterSettings();
        settings.Extensions.Add(office is null ? new OfficeExtension() : new OfficeExtension(office));
        return new Converter(settings);
    }

    /// <summary>
    /// Detects the type of a file or stream with a converter that has the Office extension.
    /// </summary>
    /// <param name="source">File or stream.</param>
    internal static Task<FileFormat> DetectAsync(Source source)
    {
        return Create().DetectAsync(source);
    }
}
