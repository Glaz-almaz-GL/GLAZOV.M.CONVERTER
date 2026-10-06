namespace GLAZOV.M.CONVERTER.Detection.Image;

/// <summary>
/// GIF: "GIF87a" or "GIF89a".
/// </summary>
internal static class GifSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith("GIF87a"u8) || head.StartsWith("GIF89a"u8);
    }
}
