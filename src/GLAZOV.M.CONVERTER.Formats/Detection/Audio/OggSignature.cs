namespace GLAZOV.M.CONVERTER.Formats.Detection.Audio;

/// <summary>
/// OGG: a page starts with "OggS" and a zero version of the stream structure.
/// </summary>
internal static class OggSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 5 && head.StartsWith("OggS"u8) && head[4] == 0;
    }
}
