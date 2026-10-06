namespace GLAZOV.M.CONVERTER.Formats.Detection.Documents;

/// <summary>
/// RTF: the file starts with the group "{\\rtf".
/// </summary>
internal static class RtfSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith("{\\rtf"u8);
    }
}
