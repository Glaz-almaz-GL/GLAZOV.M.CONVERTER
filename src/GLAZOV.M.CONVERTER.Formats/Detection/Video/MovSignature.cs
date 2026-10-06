using System.Buffers.Binary;
using System.Text;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Video;

/// <summary>
/// MOV: an ISO base media file of the brand "qt  ", or a QuickTime file without "ftyp" that starts with an atom of a known type.
/// </summary>
internal static class MovSignature
{
    // Atom types a QuickTime file without "ftyp" may start with.
    private static readonly string[] Atoms = ["moov", "mdat", "wide", "free"];

    /// <summary>
    /// True when the data starts like MOV.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (head.Length < 12)
        {
            return false;
        }

        if (head[4..8].SequenceEqual("ftyp"u8))
        {
            return head[8..12].SequenceEqual("qt  "u8);
        }

        return !EbmlHeader.StartsLikeEbml(head) && !AviSignature.Matches(head) && IsAtom(head);
    }

    // Atom: four-byte big-endian size (more than the 8-byte header) and a type from the list.
    // A size spelled entirely in printable ASCII is text, not a size; when the next atom lies inside the window
    // its type must look like a type too.
    private static bool IsAtom(ReadOnlySpan<byte> head)
    {
        uint size = BinaryPrimitives.ReadUInt32BigEndian(head[..4]);
        if (size <= 8 || IsPrintable(head[..4]))
        {
            return false;
        }

        if (size <= head.Length - 8 && !IsPrintable(head.Slice((int)size + 4, 4)))
        {
            return false;
        }

        foreach (string atom in Atoms)
        {
            if (head[4..8].SequenceEqual(Encoding.ASCII.GetBytes(atom)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPrintable(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value is < 0x20 or > 0x7E)
            {
                return false;
            }
        }

        return true;
    }
}
