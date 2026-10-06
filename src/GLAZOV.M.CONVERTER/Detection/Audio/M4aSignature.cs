namespace GLAZOV.M.CONVERTER.Detection.Audio;

/// <summary>
/// M4A: an MP4 container whose main brand is "M4A " or "M4B "; the other MP4 brands belong to video.
/// </summary>
internal static class M4aSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 12
            && head[4..8].SequenceEqual("ftyp"u8)
            && (head[8..12].SequenceEqual("M4A "u8) || head[8..12].SequenceEqual("M4B "u8));
    }
}
