using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Second look at archives and compound files of documents: the structure that names the format lies away from the start.
/// </summary>
internal sealed class ContainerDetector : IStreamFormatDetector
{
    /// <inheritdoc />
    public int HeadLength => 8;

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        return null;
    }

    /// <inheritdoc />
    public bool CanInspect(ReadOnlySpan<byte> head)
    {
        return ContainerProbe.IsContainer(head);
    }

    /// <inheritdoc />
    public FileFormat? Detect(Stream input)
    {
        return ContainerProbe.Match(input);
    }
}
