namespace GLAZOV.M.CONVERTER.Formats.Detection.Video;

/// <summary>
/// MKV: an EBML header whose DocType is "matroska".
/// </summary>
internal static class MkvSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return EbmlHeader.DocType(head) == "matroska";
    }
}
