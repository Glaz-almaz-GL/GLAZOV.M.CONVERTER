namespace GLAZOV.M.CONVERTER.ArchivesExtra.Detection;

/// <summary>
/// 7z: "37 7A BC AF 27 1C".
/// </summary>
internal static class SevenZipSignature
{
    private static readonly byte[] Magic = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(Magic);
    }
}
