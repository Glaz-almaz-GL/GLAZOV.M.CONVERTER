using GLAZOV.M.CONVERTER.Formats.Detection.Documents;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Archives;

/// <summary>
/// ZIP: a local file header ("PK 03 04") or an empty archive ("PK 05 06") that is not the package of a document. The start of a big
/// archive cannot tell a package of a document from a plain archive (the parts that name a package may lie further), so by the start alone
/// ZIP is named only when the whole file lies inside the window and shows no part of a document, or the first entry already rules a document out
/// (an entry "mimetype" that is not OpenDocument, as in EPUB; "META-INF/", as in JAR). Everything else is left to the second look,
/// which reads the central directory; an input that cannot be read back gets no answer.
/// </summary>
internal static class ZipSignature
{
    private const int HeaderLength = 30;
    private const int EmptyArchiveLength = 22;
    private const string OdfMimeStart = "application/vnd.oasis.opendocument";

    private static readonly byte[] EmptyMagic = [0x50, 0x4B, 0x05, 0x06];

    private static readonly string[] NotDocumentStarts = ["META-INF/"];

    private static readonly string[] DocumentStarts = ["[Content_Types].xml", "_rels/", "docProps/", "customXml/", "word/", "xl/", "ppt/"];

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(EmptyMagic))
        {
            return head.Length >= EmptyArchiveLength;
        }

        if (!head.StartsWith(ZipPackage.ZipMagic) || head.Length < HeaderLength || ZipPackage.Kind(head) is not null)
        {
            return false;
        }

        ushort nameLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(head[26..28]);
        if (nameLength == 0 || HeaderLength + nameLength > head.Length)
        {
            return false;
        }

        string first = System.Text.Encoding.UTF8.GetString(head.Slice(HeaderLength, nameLength));
        if (first == "mimetype")
        {
            return !StartsOdf(head, nameLength);
        }

        if (NotDocumentStarts.Any(start => first.StartsWith(start, StringComparison.Ordinal)))
        {
            return true;
        }

        if (DocumentStarts.Any(start => first.StartsWith(start, StringComparison.Ordinal)))
        {
            return false;
        }

        (bool contentTypes, HashSet<Data.FileFormat> mainParts, HashSet<Data.FileFormat> folders) = ZipPackage.Scan(head);
        return !contentTypes && mainParts.Count == 0 && folders.Count == 0 && IsWhole(head);
    }

    // The end-of-central-directory record ("PK 05 06", a comment that reaches exactly the end of the data) shows the window holds the whole file.
    private static bool IsWhole(ReadOnlySpan<byte> head)
    {
        int lowest = Math.Max(0, head.Length - (EmptyArchiveLength + ushort.MaxValue));
        for (int at = head.Length - EmptyArchiveLength; at >= lowest; at--)
        {
            if (head[at..].StartsWith(EmptyMagic) && at + EmptyArchiveLength + System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(head.Slice(at + 20, 2)) == head.Length)
            {
                return true;
            }
        }

        return false;
    }

    // The "mimetype" entry is stored first; an archive whose entry is cut off by the window is left to the second look.
    private static bool StartsOdf(ReadOnlySpan<byte> head, int nameLength)
    {
        ushort extraLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(head[28..30]);
        int dataStart = HeaderLength + nameLength + extraLength;
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(head[18..22]);
        if (dataStart + size > head.Length || size > 100)
        {
            return true;
        }

        return System.Text.Encoding.ASCII.GetString(head.Slice(dataStart, (int)size)).StartsWith(OdfMimeStart, StringComparison.Ordinal);
    }
}
