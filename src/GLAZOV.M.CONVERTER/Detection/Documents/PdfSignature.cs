namespace GLAZOV.M.CONVERTER.Detection.Documents;

/// <summary>
/// PDF: the file starts with "%PDF-".
/// </summary>
internal static class PdfSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith("%PDF-"u8);
    }
}
