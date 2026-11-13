namespace GLAZOV.M.CONVERTER.Formats.Detection.Tables;

/// <summary>
/// TSV by content: at least three rows with the same non-zero number of tabs outside quotes. A guess from the start of the data, not a proof.
/// </summary>
internal static class TsvSignature
{
    /// <summary>
    /// True when the leading bytes read as tab-separated rows.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return DelimitedText.TryFind(head, ['\t'], out _);
    }
}
