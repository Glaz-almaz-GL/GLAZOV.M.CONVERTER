using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Ffmpeg;

/// <summary>
/// Extension "FFmpeg": audio, image and video engines that run the external FFmpeg program.
/// Plugged in through <c>ConverterSettings.Extensions</c>; FFmpeg itself must be installed.
/// </summary>
public sealed class FfmpegExtension : IConverterExtension
{
    /// <summary>
    /// Creates the extension with the default settings.
    /// </summary>
    public FfmpegExtension()
        : this(new FfmpegSettings())
    {
    }

    /// <summary>
    /// Creates the extension with the given settings. A copy is taken: later changes to <paramref name="settings"/> have no effect.
    /// </summary>
    /// <param name="settings">Defaults for quality, background and VP9 speed.</param>
    public FfmpegExtension(FfmpegSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        FfmpegSettings copy = settings.Copy();
        Engines = [new FfmpegAudioEngine(copy), new FfmpegImageEngine(copy), new FfmpegVideoEngine(copy)];
    }

    /// <inheritdoc />
    public string Name => "FFmpeg";

    /// <inheritdoc />
    public IReadOnlyList<FileFormat> Formats => [.. Engines.SelectMany(engine => engine.Pairs).SelectMany(pair => new[] { pair.From, pair.To }).Distinct()];

    /// <inheritdoc />
    /// <remarks>
    /// Audio, images and video: the detectors of the standard formats from the Formats package.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [StandardDetectors.Image, StandardDetectors.Video, StandardDetectors.Audio];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; }
}
