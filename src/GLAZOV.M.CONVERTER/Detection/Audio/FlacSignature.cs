namespace GLAZOV.M.CONVERTER.Detection.Audio;

/// <summary>
/// FLAC: the stream starts with "fLaC".
/// </summary>
internal static class FlacSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.StartsWith("fLaC"u8);
    }
}
