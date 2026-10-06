using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection.Documents;

/// <summary>
/// PPT: PowerPoint presentation in a compound file (stream PowerPoint Document), when its directory lies inside the window.
/// </summary>
internal static class PptSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return CompoundFile.Kind(head)?.Equals(FileFormat.Ppt) == true;
    }
}
