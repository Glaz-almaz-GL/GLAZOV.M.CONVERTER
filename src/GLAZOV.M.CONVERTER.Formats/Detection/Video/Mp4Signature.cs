using System.Text;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Video;

/// <summary>
/// MP4: an ISO base media file whose main brand names an MP4 container. M4A brands belong to audio; 3GP and others are not guessed.
/// </summary>
internal static class Mp4Signature
{
    private static readonly string[] Brands = ["isom", "iso2", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "dash", "msnv", "f4v "];

    /// <summary>
    /// True when the data starts like MP4.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (head.Length < 12 || !head[4..8].SequenceEqual("ftyp"u8))
        {
            return false;
        }

        ReadOnlySpan<byte> brand = head[8..12];
        foreach (string known in Brands)
        {
            if (brand.SequenceEqual(Encoding.ASCII.GetBytes(known)))
            {
                return true;
            }
        }

        return false;
    }
}
