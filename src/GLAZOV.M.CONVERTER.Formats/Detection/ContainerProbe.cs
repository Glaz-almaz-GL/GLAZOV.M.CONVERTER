using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats.Detection.Documents;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace GLAZOV.M.CONVERTER.Formats.Detection;

/// <summary>
/// Deep container inspector for seekable streams: inspects ZIP central directories (at the end of the file)
/// and OLE2 compound file directory sector chains to identify document packages without relying on the header window.
/// </summary>
internal static class ContainerProbe
{
    private const string OdfMimeType = "mimetype";
    private const int MaxOdfMimeLength = 100;
    private const int HeaderTableEntries = 109;
    private const int MagicLength = 8;
    private const uint EndOfChain = 0xFFFFFFFE;

    /// <summary>
    /// Determines whether the leading bytes match a container format supported by this probe (ZIP or OLE2 compound file).
    /// </summary>
    /// <param name="head">Leading bytes of the input.</param>
    internal static bool IsContainer(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(ZipPackage.ZipMagic) || head.StartsWith(CompoundFile.CompoundMagic);
    }

    /// <summary>
    /// Asynchronously inspects the container stream and returns the identified document <see cref="FileFormat"/>, or <c>null</c>.
    /// </summary>
    /// <param name="stream">Seekable input stream positioned at the start of the container.</param>
    /// <param name="cancellationToken">Token to cancel the inspection.</param>
    internal static async ValueTask<FileFormat?> MatchAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] start = new byte[MagicLength];
            stream.Position = 0;
            int read = await ReadAtMostAsync(stream, start, cancellationToken).ConfigureAwait(false);

            if (read == start.Length && start.AsSpan().StartsWith(CompoundFile.CompoundMagic))
            {
                return await MatchCompoundAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            // Check zip magic bytes
            return read >= ZipPackage.ZipMagic.Length && start.AsSpan().StartsWith(ZipPackage.ZipMagic)
                ? await MatchArchiveAsync(stream, cancellationToken).ConfigureAwait(false)
                : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    private static async ValueTask<FileFormat?> MatchArchiveAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await using ZipArchive zip = new(stream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 0 && zip.Entries[0].FullName == OdfMimeType)
        {
            return await MatchOdfAsync(zip.Entries[0], cancellationToken).ConfigureAwait(false);
        }

        bool contentTypes = false;
        HashSet<FileFormat> mainParts = [];
        HashSet<FileFormat> folders = [];
        foreach (string entryFullName in zip.Entries.Select(entry => entry.FullName))
        {
            contentTypes |= entryFullName == ZipPackage.ContentTypesPart;
            if (ZipPackage.MainPart(entryFullName) is { } main)
            {
                mainParts.Add(main);
            }

            if (ZipPackage.Folder(entryFullName) is { } folder)
            {
                folders.Add(folder);
            }
        }

        return ZipPackage.ChooseOffice(contentTypes, mainParts, folders);
    }

    private static async ValueTask<FileFormat?> MatchOdfAsync(ZipArchiveEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Length > MaxOdfMimeLength)
        {
            return null;
        }

        await using Stream content = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[] buffer = new byte[MaxOdfMimeLength];
        int total = await ReadAtMostAsync(content, buffer, cancellationToken).ConfigureAwait(false);
        return ZipPackage.OdfFormat(Encoding.ASCII.GetString(buffer, 0, total));
    }

    private static async ValueTask<FileFormat?> MatchCompoundAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[CompoundFile.CompoundHeaderLength];
        stream.Position = 0;
        if (!await ReadFullyAsync(stream, header, cancellationToken).ConfigureAwait(false))
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

        List<uint> tables = await ReadTableSectorsAsync(stream, header, sectorSize, tableCount, firstExtra, extraCount, cancellationToken).ConfigureAwait(false);
        if (tables.Count == 0)
        {
            return null;
        }

        byte[]? directory = await ReadDirectoryAsync(stream, firstDirectory, sectorSize, tables, cancellationToken).ConfigureAwait(false);
        return directory is null ? null : CompoundFile.MatchDirectory(directory);
    }

    private static async ValueTask<List<uint>> ReadTableSectorsAsync(
        Stream stream,
        byte[] header,
        int sectorSize,
        uint tableCount,
        uint firstExtra,
        uint extraCount,
        CancellationToken cancellationToken = default)
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
            if (!await ReadSectorAsync(stream, next, sector, cancellationToken).ConfigureAwait(false))
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

    private static async ValueTask<byte[]?> ReadDirectoryAsync(
        Stream stream,
        uint first,
        int sectorSize,
        List<uint> tables,
        CancellationToken cancellationToken = default)
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
            if (!await ReadSectorAsync(stream, current, sector, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            await directory.WriteAsync(sector, cancellationToken);

            uint tableIndex = current / (uint)perTable;
            if (tableIndex >= tables.Count)
            {
                break;
            }

            uint table = tables[(int)tableIndex];
            if (!loaded.TryGetValue(table, out byte[]? entries))
            {
                entries = new byte[sectorSize];
                if (!await ReadSectorAsync(stream, table, entries, cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                loaded[table] = entries;
            }

            current = BinaryPrimitives.ReadUInt32LittleEndian(entries.AsSpan((int)(current % (uint)perTable) * 4, 4));
        }

        return directory.Length == 0 ? null : directory.ToArray();
    }

    private static async ValueTask<bool> ReadSectorAsync(Stream stream, uint id, byte[] sector, CancellationToken cancellationToken = default)
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
        return await ReadFullyAsync(stream, sector, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<bool> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken = default)
    {
        return await ReadAtMostAsync(stream, buffer, cancellationToken).ConfigureAwait(false) == buffer.Length;
    }

    private static async ValueTask<int> ReadAtMostAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken = default)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
