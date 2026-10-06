using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Formats.Tests;

/// <summary>
/// Creates a converter with the Formats extension plugged in: it knows the types and has no engine.
/// </summary>
internal static class FormatsConverter
{
    /// <summary>
    /// A converter with <see cref="FormatsExtension"/>; <paramref name="settings"/> are used when given.
    /// </summary>
    internal static Converter Create(ConverterSettings? settings = null)
    {
        settings ??= new ConverterSettings();
        settings.Extensions.Add(new FormatsExtension());
        return new Converter(settings);
    }
}
