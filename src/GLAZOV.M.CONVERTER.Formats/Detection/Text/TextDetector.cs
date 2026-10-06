using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Text;

/// <summary>
/// Plain text by content, to be connected after every other detector by content: data that no other sign claimed and that reads as text is <see cref="DocumentFormats.Text"/>.
/// </summary>
internal sealed class TextDetector : IFormatDetector
{
    /// <inheritdoc />
    public int HeadLength => DetectionSizes.MinimumWindow;

    /// <inheritdoc />
    public bool ByContent => true;

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        return TextSignature.Matches(head) ? FileFormat.Text : null;
    }
}
