using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Documents;

/// <summary>
/// Second look at the containers of documents, for an input that can be read at any place: an archive by its central directory
/// (at the end of the file) and a compound file by the chain of its directory sectors, wherever they lie. Never throws on short or
/// foreign data; what cannot be read inside the budget is not guessed.
/// </summary>
internal static class ContainerProbe
{
    private const string OdfMimeType = "mimetype";
    private const int MaxOdfMimeLength = 100;
    private const int HeaderTableEntries = 109;
    private const int MagicLength = 8;
    private const uint EndOfChain = 0xFFFFFFFE;

    /// <summary>
    /// Whether the data starts like a container the probe can open.
    /// </summary>
    /// <param name="head">Leading bytes of the input.</param>
    internal static bool IsContainer(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(ZipPackage.ZipMagic) || head.StartsWith(CompoundFile.CompoundMagic);
    }

    /// <summary>
    /// Returns the document format of the container, or <c>null</c>.
    /// </summary>
    /// <param name="stream">The input; position 0 is its start.</param>
    internal static FileFormat? Match(Stream stream)
    {
        try
        {
            byte[] start = new byte[MagicLength];
            stream.Position = 0;
            int read = stream.Read(start, 0, start.Length);
            if (read == start.Length && start.AsSpan().StartsWith(CompoundFile.CompoundMagic))
            {
                return MatchCompound(stream);
            }

            return read >= ZipPackage.ZipMagic.Length && start.AsSpan().StartsWith(ZipPackage.ZipMagic) ? MatchArchive(stream) : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    private static FileFormat? MatchArchive(Stream stream)
    {
        using ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 0 && zip.Entries[0].FullName == OdfMimeType)
        {
            return MatchOdf(zip.Entries[0]);
        }

        bool contentTypes = false;
        HashSet<FileFormat> mainParts = [];
        HashSet<FileFormat> folders = [];
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            contentTypes |= entry.FullName == ZipPackage.ContentTypesPart;
            if (ZipPackage.MainPart(entry.FullName) is { } main)
            {
                mainParts.Add(main);
            }

            if (ZipPackage.Folder(entry.FullName) is { } folder)
            {
                folders.Add(folder);
            }
        }

        return ZipPackage.ChooseOffice(contentTypes, mainParts, folders);
    }

    private static FileFormat? MatchOdf(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxOdfMimeLength)
        {
            return null;
        }

        using Stream content = entry.Open();
        byte[] buffer = new byte[MaxOdfMimeLength];
        int total = 0;
        int read;
        while (total < buffer.Length && (read = content.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += read;
        }

        return ZipPackage.OdfFormat(Encoding.ASCII.GetString(buffer, 0, total));
    }

    // The header names the first directory sector; the sector allocation table (found through the header and the chain of extra
    // table sectors) gives the next sector of the directory. Only the table sectors that are needed are read.
    private static FileFormat? MatchCompound(Stream stream)
    {
        byte[] header = new byte[CompoundFile.CompoundHeaderLength];
        stream.Position = 0;
        if (!ReadFully(stream, header))
        {
            return null;
        }

        int shift = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(0x1E, 2));
        if (shift is not (9 or 12))
        {
            return null;
        }

        int sectorSize = 1 << shift;
        uint tableCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x2C, 4));
        uint firstDirectory = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x30, 4));
        uint firstExtra = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x44, 4));
        uint extraCount = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x48, 4));

        List<uint> tables = ReadTableSectors(stream, header, sectorSize, tableCount, firstExtra, extraCount);
        if (tables.Count == 0)
        {
            return null;
        }

        byte[]? directory = ReadDirectory(stream, firstDirectory, sectorSize, tables);
        return directory is null ? null : CompoundFile.MatchDirectory(directory);
    }

    private static List<uint> ReadTableSectors(Stream stream, byte[] header, int sectorSize, uint tableCount, uint firstExtra, uint extraCount)
    {
        List<uint> tables = [];
        for (int i = 0; i < HeaderTableEntries && tables.Count < tableCount; i++)
        {
            tables.Add(BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x4C + (i * 4), 4)));
        }

        byte[] sector = new byte[sectorSize];
        int entries = (sectorSize / 4) - 1;
        uint next = firstExtra;
        for (uint i = 0; i < extraCount && tables.Count < tableCount && next < EndOfChain; i++)
        {
            if (!ReadSector(stream, next, sector))
            {
                break;
            }

            for (int e = 0; e < entries && tables.Count < tableCount; e++)
            {
                tables.Add(BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(e * 4, 4)));
            }

            next = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(entries * 4, 4));
        }

        return tables;
    }

    private static byte[]? ReadDirectory(Stream stream, uint first, int sectorSize, List<uint> tables)
    {
        int perTable = sectorSize / 4;
        int maxSectors = Math.Max(1, CompoundFile.MaxDirectoryEntries * CompoundFile.CompoundEntryLength / sectorSize);
        Dictionary<uint, byte[]> loaded = [];
        HashSet<uint> seen = [];
        using MemoryStream directory = new();
        byte[] sector = new byte[sectorSize];
        uint current = first;
        while (current < EndOfChain && seen.Count < maxSectors && seen.Add(current))
        {
            if (!ReadSector(stream, current, sector))
            {
                return null;
            }

            directory.Write(sector);

            uint tableIndex = current / (uint)perTable;
            if (tableIndex >= tables.Count)
            {
                break;
            }

            uint table = tables[(int)tableIndex];
            if (!loaded.TryGetValue(table, out byte[]? entries))
            {
                entries = new byte[sectorSize];
                if (!ReadSector(stream, table, entries))
                {
                    return null;
                }

                loaded[table] = entries;
            }

            current = BinaryPrimitives.ReadUInt32LittleEndian(entries.AsSpan((int)(current % (uint)perTable) * 4, 4));
        }

        return directory.Length == 0 ? null : directory.ToArray();
    }

    // Sector n starts after the header sector, at (n + 1) << shift.
    private static bool ReadSector(Stream stream, uint id, byte[] sector)
    {
        if (id >= EndOfChain)
        {
            return false;
        }

        long offset = ((long)id + 1) * sector.Length;
        if (offset + sector.Length > stream.Length)
        {
            return false;
        }

        stream.Position = offset;
        return ReadFully(stream, sector);
    }

    private static bool ReadFully(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }
}
