namespace GLAZOV.M.CONVERTER.Detection.Image;

/// <summary>
/// TIFF: "II*\0" (Intel byte order) or "MM\0*" (Motorola byte order).
/// </summary>
internal static class TiffSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith((ReadOnlySpan<byte>)[0x49, 0x49, 0x2A, 0x00]) || head.StartsWith((ReadOnlySpan<byte>)[0x4D, 0x4D, 0x00, 0x2A]);
    }
}
