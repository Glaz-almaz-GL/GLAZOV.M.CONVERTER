namespace GLAZOV.M.CONVERTER.Formats.Detection.Video;

/// <summary>
/// WebM: an EBML header whose DocType is "webm".
/// </summary>
internal static class WebmSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return EbmlHeader.DocType(head) == "webm";
    }
}
