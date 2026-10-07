namespace GLAZOV.M.CONVERTER.ArchivesExtra.Detection;

/// <summary>
/// BZip2: "BZh", the block size "1" to "9" and the block magic 31 41 59 26 53 59.
/// </summary>
internal static class Bzip2Signature
{
    private static readonly byte[] BlockMagic = [0x31, 0x41, 0x59, 0x26, 0x53, 0x59];

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 10 && head.StartsWith("BZh"u8) && head[3] is >= (byte)'1' and <= (byte)'9' && head[4..10].SequenceEqual(BlockMagic);
    }
}
