namespace GLAZOV.M.CONVERTER.Detection.Image;

/// <summary>
/// JPEG: the marker of the start of the image followed by another marker.
/// </summary>
internal static class JpegSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]);
    }
}
