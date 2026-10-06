using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests;

/// <summary>
/// Creates a converter with the FFmpeg extension plugged in, the way a user of the package does.
/// </summary>
internal static class FfmpegConverter
{
    /// <summary>
    /// A converter with <see cref="FfmpegExtension"/>; <paramref name="settings"/> are used when given.
    /// </summary>
    internal static Converter Create(ConverterSettings? settings = null)
    {
        settings ??= new ConverterSettings();
        settings.Extensions.Add(new FfmpegExtension());
        return new Converter(settings);
    }
}
