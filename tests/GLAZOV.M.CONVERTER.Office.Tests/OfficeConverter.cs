using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Office.Tests;

/// <summary>
/// Creates a converter with the Office extension plugged in, the way a user of the package does.
/// </summary>
internal static class OfficeConverter
{
    /// <summary>
    /// A converter with <see cref="OfficeExtension"/>; <paramref name="settings"/> are used when given.
    /// </summary>
    internal static Converter Create(ConverterSettings? settings = null)
    {
        settings ??= new ConverterSettings();
        settings.Extensions.Add(new OfficeExtension());
        return new Converter(settings);
    }
}
