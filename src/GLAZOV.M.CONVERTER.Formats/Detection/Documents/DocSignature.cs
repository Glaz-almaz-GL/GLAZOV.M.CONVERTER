using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Documents;

/// <summary>
/// DOC: Word document in a compound file (stream WordDocument), when its directory lies inside the window.
/// </summary>
internal static class DocSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return CompoundFile.Kind(head)?.Equals(FileFormat.Doc) == true;
    }
}
