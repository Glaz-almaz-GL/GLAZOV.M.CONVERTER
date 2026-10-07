namespace GLAZOV.M.CONVERTER.ArchivesExtra.Detection;

/// <summary>
/// RAR: "Rar!" 1A 07 00 (version 4) or "Rar!" 1A 07 01 00 (version 5).
/// </summary>
internal static class RarSignature
{
    private static readonly byte[] Version4 = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00];
    private static readonly byte[] Version5 = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00];

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(Version4) || head.StartsWith(Version5);
    }
}
