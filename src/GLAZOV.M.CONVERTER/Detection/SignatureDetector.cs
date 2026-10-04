using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Готовые признаки библиотеки (звук и изображения) как обычный определитель формата: подключается тем же путём, что и чужие.
/// </summary>
/// <param name="window">Окно определения в байтах — настройка <see cref="ConverterSettings.DetectionWindow"/>.</param>
internal sealed class SignatureDetector(int window = ConverterSettings.DefaultDetectionWindow) : IFormatDetector
{
    /// <inheritdoc />
    public int HeadLength { get; } = window;

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        FileFormat found = SignatureMatcher.Match(head, HeadLength);
        return found.Equals(FileFormat.Unknown) ? null : found;
    }
}
