using System.Buffers.Binary;
using System.Text;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Documents;

/// <summary>
/// What an archive (ZIP) holds that names its document format: ODT, ODS, ODP by the first entry "mimetype", DOCX, XLSX, PPTX by the entries
/// that lie inside the detection window. Never throws on short or foreign data; what cannot be confirmed inside the window is not guessed.
/// </summary>
internal static class ZipPackage
{
    internal static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    private static readonly byte[] DescriptorMagic = [0x50, 0x4B, 0x07, 0x08];

    private const int ZipHeaderLength = 30;

    // The folder of an OOXML package names its kind; the entries come in no fixed order, so any entry inside the window shows it.
    internal const string ContentTypesPart = "[Content_Types].xml";

    private const string WordFolder = "word/";

    private const string ExcelFolder = "xl/";

    private const string PowerPointFolder = "ppt/";

    /// <summary>
    /// Returns the document format of an archive the data starts like, or <c>null</c>.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static FileFormat? Kind(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(ZipMagic) ? MatchArchive(head) : null;
    }

    // ODF has "mimetype" first and stored. Other archives are searched for local file headers inside the window: a writer that
    // streams its output hides the entry sizes (data descriptor), so the headers cannot be walked, but each one starts with the magic
    // and carries the name. An Office Open XML package is confirmed by its main part, or by the content types entry together
    // with an entry of the folder of one kind: a plain archive with a folder called "word/" is not a document.
    private static FileFormat? MatchArchive(ReadOnlySpan<byte> head)
    {
        FileFormat? odf = MatchOdf(head);
        if (odf is not null)
        {
            return odf;
        }

        bool contentTypes = false;
        HashSet<FileFormat> mainParts = [];
        HashSet<FileFormat> folders = [];
        int offset = 0;
        while (offset + ZipHeaderLength <= head.Length && head[offset..].StartsWith(ZipMagic))
        {
            ReadOnlySpan<byte> header = head[offset..];
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]);
            uint compressed = BinaryPrimitives.ReadUInt32LittleEndian(header[18..22]);
            ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[26..28]);
            ushort extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..30]);
            int dataStart = ZipHeaderLength + nameLength + extraLength;
            if (nameLength == 0 || offset + dataStart > head.Length)
            {
                break;
            }

            string name = Encoding.UTF8.GetString(header.Slice(ZipHeaderLength, nameLength));
            contentTypes |= name == ContentTypesPart;
            if (MainPart(name) is { } main)
            {
                mainParts.Add(main);
            }

            if (Folder(name) is { } folder)
            {
                folders.Add(folder);
            }

            if ((flags & 0x08) == 0)
            {
                // The size is known: skip the data, so an archive stored inside is not read as part of this one.
                if (compressed == uint.MaxValue || (long)offset + dataStart + compressed > head.Length)
                {
                    break;
                }

                offset += dataStart + (int)compressed;
                continue;
            }

            // A data descriptor hides the size of streamed data. The end of the data is found by the descriptor itself: its signature
            // followed by the compressed size, which must equal the distance from the start of the data. Bytes of an archive stored
            // inside carry descriptors of their own whose sizes do not fit this distance, so they are not mistaken for ours.
            int next = FindAfterDescriptor(header, dataStart);
            if (next < 0)
            {
                break;
            }

            offset += next;
        }

        return ChooseOffice(contentTypes, mainParts, folders);
    }

    /// <summary>
    /// Decides the Office Open XML kind from what an archive holds: one main part, or the content types entry with one kind of folder.
    /// </summary>
    internal static FileFormat? ChooseOffice(bool contentTypes, HashSet<FileFormat> mainParts, HashSet<FileFormat> folders)
    {
        if (mainParts.Count == 1)
        {
            return mainParts.Single();
        }

        return contentTypes && mainParts.Count == 0 && folders.Count == 1 ? folders.Single() : null;
    }

    // Offset (from the header) of the next local header after the data descriptor of a streamed entry, or -1.
    private static int FindAfterDescriptor(ReadOnlySpan<byte> header, int dataStart)
    {
        int from = dataStart;
        while (from < header.Length)
        {
            int at = header[from..].IndexOf(DescriptorMagic);
            if (at < 0)
            {
                return -1;
            }

            int descriptor = from + at;
            long distance = descriptor - dataStart;
            if (descriptor + 16 <= header.Length && BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(descriptor + 8, 4)) == distance
                && header[(descriptor + 16)..].StartsWith(ZipMagic))
            {
                return descriptor + 16;
            }

            if (descriptor + 24 <= header.Length && BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(descriptor + 8, 8)) == (ulong)distance
                && header[(descriptor + 24)..].StartsWith(ZipMagic))
            {
                return descriptor + 24;
            }

            from = descriptor + DescriptorMagic.Length;
        }

        return -1;
    }

    private static FileFormat? MatchOdf(ReadOnlySpan<byte> head)
    {
        if (head.Length < ZipHeaderLength)
        {
            return null;
        }

        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(head[6..8]);
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(head[18..22]);
        ushort nameLength = BinaryPrimitives.ReadUInt16LittleEndian(head[26..28]);
        ushort extraLength = BinaryPrimitives.ReadUInt16LittleEndian(head[28..30]);
        int dataStart = ZipHeaderLength + nameLength + extraLength;
        if ((flags & 0x08) != 0 || dataStart > head.Length || !head.Slice(ZipHeaderLength, nameLength).SequenceEqual("mimetype"u8))
        {
            return null;
        }

        return MatchMimeType(head, dataStart, size);
    }

    internal static FileFormat? MainPart(string name)
    {
        return name switch
        {
            "word/document.xml" => FileFormat.Docx,
            "xl/workbook.xml" => FileFormat.Xlsx,
            "ppt/presentation.xml" => FileFormat.Pptx,
            _ => null,
        };
    }

    internal static FileFormat? Folder(string name)
    {
        if (name.StartsWith(WordFolder, StringComparison.Ordinal))
        {
            return FileFormat.Docx;
        }

        if (name.StartsWith(ExcelFolder, StringComparison.Ordinal))
        {
            return FileFormat.Xlsx;
        }

        return name.StartsWith(PowerPointFolder, StringComparison.Ordinal) ? FileFormat.Pptx : null;
    }

    private static FileFormat? MatchMimeType(ReadOnlySpan<byte> header, int dataStart, uint size)
    {
        if (dataStart + size > header.Length || size > 100)
        {
            return null;
        }

        return OdfFormat(Encoding.ASCII.GetString(header.Slice(dataStart, (int)size)));
    }

    /// <summary>
    /// Maps the content of the "mimetype" entry of an OpenDocument package to a format.
    /// </summary>
    internal static FileFormat? OdfFormat(string mime)
    {
        return mime switch
        {
            "application/vnd.oasis.opendocument.text" => FileFormat.Odt,
            "application/vnd.oasis.opendocument.spreadsheet" => FileFormat.Ods,
            "application/vnd.oasis.opendocument.presentation" => FileFormat.Odp,
            _ => null,
        };
    }
}
