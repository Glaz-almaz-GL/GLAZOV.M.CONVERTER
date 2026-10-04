using System.Buffers.Binary;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Signatures of video containers: MP4 and MOV (ISO base media "ftyp" brand or a leading QuickTime atom),
/// MKV and WebM (EBML header with its DocType) and AVI (RIFF "AVI "). Never throws on short or foreign data.
/// </summary>
internal static class VideoSignature
{
    private static readonly byte[] EbmlMagic = [0x1A, 0x45, 0xDF, 0xA3];

    // Main brands that name an MP4 container. M4A brands are handled before this class; 3GP and others are not guessed.
    private static readonly string[] Mp4Brands = ["isom", "iso2", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "dash", "msnv", "f4v "];

    // Atom types a QuickTime file without "ftyp" may start with.
    private static readonly string[] MovAtoms = ["moov", "mdat", "wide", "free"];

    // How far after the EBML magic the DocType element is looked for.
    private const int EbmlSearchLength = 64;

    /// <summary>
    /// Returns the video format the data starts like, or <c>null</c>.
    /// </summary>
    /// <param name="head">Leading bytes of the input.</param>
    internal static FileFormat? Match(ReadOnlySpan<byte> head)
    {
        if (head.Length < 12)
        {
            return null;
        }

        if (head[4..8].SequenceEqual("ftyp"u8))
        {
            return MatchBrand(head[8..12]);
        }

        if (head.StartsWith(EbmlMagic))
        {
            return MatchDocType(head);
        }

        if (head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("AVI "u8))
        {
            return FileFormat.Avi;
        }

        return IsQuickTimeAtom(head) ? FileFormat.Mov : null;
    }

    private static FileFormat? MatchBrand(ReadOnlySpan<byte> brand)
    {
        if (brand.SequenceEqual("qt  "u8))
        {
            return FileFormat.Mov;
        }

        foreach (string known in Mp4Brands)
        {
            if (brand.SequenceEqual(System.Text.Encoding.ASCII.GetBytes(known)))
            {
                return FileFormat.Mp4;
            }
        }

        return null;
    }

    // DocType element: id 0x42 0x82, one-byte size (high bit set), then the text.
    private static FileFormat? MatchDocType(ReadOnlySpan<byte> head)
    {
        ReadOnlySpan<byte> area = head[EbmlMagic.Length..Math.Min(head.Length, EbmlMagic.Length + EbmlSearchLength)];
        for (int i = 0; i + 3 <= area.Length; i++)
        {
            if (area[i] != 0x42 || area[i + 1] != 0x82 || (area[i + 2] & 0x80) == 0)
            {
                continue;
            }

            int length = area[i + 2] & 0x7F;
            if (i + 3 + length > area.Length)
            {
                return null;
            }

            ReadOnlySpan<byte> value = area.Slice(i + 3, length);
            if (value.SequenceEqual("matroska"u8))
            {
                return FileFormat.Mkv;
            }

            if (value.SequenceEqual("webm"u8))
            {
                return FileFormat.Webm;
            }
        }

        return null;
    }

    // Atom: four-byte big-endian size (more than the 8-byte header) and a type from the list.
    // A size spelled entirely in printable ASCII is text, not a size; when the next atom lies inside the window
    // its type must look like a type too.
    private static bool IsQuickTimeAtom(ReadOnlySpan<byte> head)
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

        foreach (string atom in MovAtoms)
        {
            if (head[4..8].SequenceEqual(System.Text.Encoding.ASCII.GetBytes(atom)))
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
