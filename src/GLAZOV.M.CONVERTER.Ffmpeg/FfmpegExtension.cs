using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;

namespace GLAZOV.M.CONVERTER.Ffmpeg;

/// <summary>
/// Extension "FFmpeg": audio, image and video engines that run the external FFmpeg program.
/// Plugged in through <c>ConverterSettings.Extensions</c>; FFmpeg itself must be installed.
/// </summary>
public sealed class FfmpegExtension : IConverterExtension
{
    /// <inheritdoc />
    public string Name => "FFmpeg";

    /// <inheritdoc />
    public IReadOnlyList<FileFormat> Formats => [.. Engines.SelectMany(engine => engine.Pairs).SelectMany(pair => new[] { pair.From, pair.To }).Distinct()];

    /// <inheritdoc />
    /// <remarks>
    /// Empty: the formats are recognised by the detectors of the core library.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; } = [new FfmpegAudioEngine(), new FfmpegImageEngine(), new FfmpegVideoEngine()];
}
