namespace GLAZOV.M.CONVERTER.Detection.Image;

/// <summary>
/// WebP: a RIFF container of the type WEBP.
/// </summary>
internal static class WebpSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8);
    }
}
