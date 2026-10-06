namespace GLAZOV.M.CONVERTER.Detection.Video;

/// <summary>
/// AVI: a RIFF container of the type "AVI ".
/// </summary>
internal static class AviSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("AVI "u8);
    }
}
