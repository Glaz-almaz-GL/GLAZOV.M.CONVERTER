namespace GLAZOV.M.CONVERTER.Formats.Detection.Audio;

/// <summary>
/// WAV: a RIFF container of the type WAVE — "RIFF", four bytes of size, "WAVE".
/// </summary>
internal static class WavSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WAVE"u8);
    }
}
