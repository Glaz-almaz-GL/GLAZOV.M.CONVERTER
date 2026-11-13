namespace GLAZOV.M.CONVERTER.Formats.Detection.Tables;

/// <summary>
/// CSV by content: at least three rows with the same non-zero number of commas, or of semicolons, outside quotes. A guess from the start of the data, not a proof.
/// </summary>
internal static class CsvSignature
{
    /// <summary>
    /// True when the leading bytes read as comma- or semicolon-separated rows.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return DelimitedText.TryFind(head, [',', ';'], out _);
    }
}
