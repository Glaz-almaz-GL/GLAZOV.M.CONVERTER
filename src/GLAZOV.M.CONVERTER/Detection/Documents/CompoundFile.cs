using System.Buffers.Binary;
using System.Text;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection.Documents;

/// <summary>
/// What a compound file (OLE2) holds that names its document format: DOC, XLS, PPT by the streams of the root of its directory.
/// Never throws on short or foreign data.
/// </summary>
internal static class CompoundFile
{
    internal static readonly byte[] CompoundMagic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    internal const int CompoundHeaderLength = 512;

    internal const int CompoundEntryLength = 128;

    internal const int MaxDirectoryEntries = 4096;

    private const byte StreamObject = 2;

    /// <summary>
    /// Returns the document format of a compound file the data starts like, or <c>null</c>.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static FileFormat? Kind(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(CompoundMagic) ? MatchCompound(head) : null;
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
}
