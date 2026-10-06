namespace GLAZOV.M.CONVERTER.Formats.Detection.Image;

/// <summary>
/// PSD: "8BPS", version 1 (PSD) or 2 (PSB), six zero bytes.
/// </summary>
internal static class PsdSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 12
            && head.StartsWith("8BPS"u8)
            && head[4] == 0
            && head[5] is 1 or 2
            && head[6..12].IndexOfAnyExcept((byte)0) < 0;
    }
}
