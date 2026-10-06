using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Detection.Text;

/// <summary>
/// The last detector of every chain: data that no other sign claimed and that reads as text is <see cref="FileFormat.Text"/>.
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
