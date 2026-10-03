using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Готовые признаки библиотеки (звук и изображения) как обычный определитель формата: подключается тем же путём, что и чужие.
/// </summary>
internal sealed class SignatureDetector : IFormatDetector
{
    /// <inheritdoc />
    public int HeadLength => SignatureMatcher.HeadLength;

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        FileFormat found = SignatureMatcher.Match(head);
        return found.Equals(FileFormat.Unknown) ? null : found;
    }
}
