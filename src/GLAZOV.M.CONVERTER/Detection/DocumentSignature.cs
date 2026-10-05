using System.Buffers.Binary;
using System.Text;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Signatures of documents: PDF, RTF, HTML, archive formats (DOCX, XLSX, PPTX by the folder of an entry, ODT, ODS, ODP by the first entry "mimetype") by the entries of the
/// archive that lie inside the detection window, and the old Office formats (DOC, XLS, PPT) by the directory of the
/// compound file when it lies inside the window. Never throws on short or foreign data; what cannot be confirmed
/// inside the window is not guessed.
/// </summary>
internal static class DocumentSignature
{
    internal static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] DescriptorMagic = [0x50, 0x4B, 0x07, 0x08];
    internal static readonly byte[] CompoundMagic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private const int ZipHeaderLength = 30;
    internal const int CompoundHeaderLength = 512;
    internal const int CompoundEntryLength = 128;
    internal const int MaxDirectoryEntries = 4096;
    private const byte StreamObject = 2;

    // The folder of an OOXML package names its kind; the entries come in no fixed order, so any entry inside the window shows it.
    internal const string ContentTypesPart = "[Content_Types].xml";
    private const string WordFolder = "word/";
    private const string ExcelFolder = "xl/";
    private const string PowerPointFolder = "ppt/";

    /// <summary>
    /// Returns the document format the data starts like, or <c>null</c>.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static FileFormat? Match(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith("%PDF-"u8))
        {
            return FileFormat.Pdf;
        }

        if (head.StartsWith("{\\rtf"u8))
        {
            return FileFormat.Rtf;
        }

        if (head.StartsWith(ZipMagic))
        {
            return MatchArchive(head);
        }

        if (head.StartsWith(CompoundMagic))
        {
            return MatchCompound(head);
        }

        return IsHtml(head) ? FileFormat.Html : null;
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

    // Compound file: sector size and the first directory sector are in the header; the directory entries (128 bytes each, numbered
    // from 0, entry 0 is the root) form a tree. Only the direct children of the root decide: a stream with the same name inside a
    // nested storage (an embedded object) does not. The entries read are the ones that follow the first directory sector inside
    // the window, which is how a small file lays its directory out.
    private static FileFormat? MatchCompound(ReadOnlySpan<byte> head)
    {
        if (head.Length < CompoundHeaderLength)
        {
            return null;
        }

        ushort shift = BinaryPrimitives.ReadUInt16LittleEndian(head[0x1E..0x20]);
        uint firstDirectorySector = BinaryPrimitives.ReadUInt32LittleEndian(head[0x30..0x34]);
        if (shift is not (9 or 12))
        {
            return null;
        }

        long offset = ((long)firstDirectorySector + 1) << shift;
        if (offset + CompoundEntryLength > head.Length)
        {
            return null;
        }

        return MatchDirectory(head[(int)offset..]);
    }

    /// <summary>
    /// Names the old Office format by the streams that are direct children of the root of a compound file directory.
    /// </summary>
    /// <param name="directory">Directory entries from entry 0 (the root) on.</param>
    internal static FileFormat? MatchDirectory(ReadOnlySpan<byte> directory)
    {
        int count = Math.Min(directory.Length / CompoundEntryLength, MaxDirectoryEntries);
        if (count == 0)
        {
            return null;
        }

        HashSet<string> streams = [];
        Stack<uint> pending = new();
        HashSet<uint> seen = [];
        pending.Push(Child(directory, 0));
        while (pending.Count > 0)
        {
            uint id = pending.Pop();
            if (id >= count || !seen.Add(id))
            {
                continue;
            }

            ReadOnlySpan<byte> entry = directory.Slice((int)id * CompoundEntryLength, CompoundEntryLength);
            if (entry[0x42] == StreamObject)
            {
                streams.Add(EntryName(entry));
            }

            pending.Push(BinaryPrimitives.ReadUInt32LittleEndian(entry[0x44..0x48]));
            pending.Push(BinaryPrimitives.ReadUInt32LittleEndian(entry[0x48..0x4C]));
        }

        if (streams.Contains("WordDocument"))
        {
            return FileFormat.Doc;
        }

        if (streams.Contains("PowerPoint Document"))
        {
            return FileFormat.Ppt;
        }

        return streams.Contains("Workbook") || streams.Contains("Book") ? FileFormat.Xls : null;
    }

    private static uint Child(ReadOnlySpan<byte> directory, int id)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice((id * CompoundEntryLength) + 0x4C, 4));
    }

    // Name of a directory entry: UTF-16 in the first 64 bytes, its length (with the end mark) at 0x40.
    private static string EntryName(ReadOnlySpan<byte> entry)
    {
        int length = BinaryPrimitives.ReadUInt16LittleEndian(entry[0x40..0x42]);
        return length is >= 2 and <= 64 ? Encoding.Unicode.GetString(entry[..(length - 2)]) : string.Empty;
    }

    // After spaces and a UTF-8 byte order mark: the start of markup, without regard to case.
    private static bool IsHtml(ReadOnlySpan<byte> head)
    {
        ReadOnlySpan<byte> rest = head.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? head[3..] : head;
        int start = 0;
        while (start < rest.Length && rest[start] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            start++;
        }

        ReadOnlySpan<byte> text = rest[start..];
        foreach (string prefix in new[] { "<!doctype html", "<html", "<head", "<body" })
        {
            if (text.Length >= prefix.Length
                && Encoding.ASCII.GetString(text[..prefix.Length]).Equals(prefix, StringComparison.OrdinalIgnoreCase)
                && (text.Length == prefix.Length || text[prefix.Length] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'>' or (byte)'/'))
            {
                return true;
            }
        }

        return false;
    }
}
