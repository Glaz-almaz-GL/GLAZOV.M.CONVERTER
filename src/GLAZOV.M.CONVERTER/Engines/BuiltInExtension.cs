using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Готовые возможности библиотеки — звук, изображения и видео через FFmpeg — как расширение: подключаются тем же путём, что и чужие.
/// </summary>
/// <param name="detectionWindow">Окно определения формата в байтах — настройка <c>ConverterSettings.DetectionWindow</c>.</param>
internal sealed class BuiltInExtension(int detectionWindow = ConverterSettings.DefaultDetectionWindow) : IConverterExtension
{
    /// <inheritdoc />
    public string Name => "Встроенные";

    /// <inheritdoc />
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [new SignatureDetector(detectionWindow)];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; } = [new FfmpegAudioEngine(), new FfmpegImageEngine(), new FfmpegVideoEngine(), new LibreOfficeEngine()];
}
