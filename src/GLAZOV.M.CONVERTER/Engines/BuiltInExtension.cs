using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Готовые возможности библиотеки — звук и изображения через FFmpeg — как расширение: подключаются тем же путём, что и чужие.
/// </summary>
internal sealed class BuiltInExtension : IConverterExtension
{
    /// <inheritdoc />
    public string Name => "Встроенные";

    /// <inheritdoc />
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [new SignatureDetector()];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; } = [new FfmpegAudioEngine(), new FfmpegImageEngine()];
}
