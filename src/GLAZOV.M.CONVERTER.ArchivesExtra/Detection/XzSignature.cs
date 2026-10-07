namespace GLAZOV.M.CONVERTER.ArchivesExtra.Detection;

/// <summary>
/// XZ: "FD 37 7A 58 5A 00".
/// </summary>
internal static class XzSignature
{
    internal static readonly byte[] Magic = [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00];

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(Magic);
    }
}
