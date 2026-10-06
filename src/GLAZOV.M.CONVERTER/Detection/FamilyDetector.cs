using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// A detector of one family of formats (audio, images, video, documents): its signs are tried in the order of the list, the first that fits names the format.
/// </summary>
/// <param name="signatures">Formats with the tests of their signs, in the order of asking.</param>
internal abstract class FamilyDetector((FileFormat Format, SignatureTest Matches)[] signatures) : IFormatDetector
{
    /// <inheritdoc />
    public virtual int HeadLength => DetectionSizes.MinimumWindow;

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        foreach ((FileFormat format, SignatureTest matches) in signatures)
        {
            if (matches(head))
            {
                return format;
            }
        }

        return null;
    }
}
