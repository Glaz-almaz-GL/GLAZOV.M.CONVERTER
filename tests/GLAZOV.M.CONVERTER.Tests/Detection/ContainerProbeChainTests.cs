using System.Buffers.Binary;
using System.Text;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.IO;

namespace GLAZOV.M.CONVERTER.Tests.Detection;

/// <summary>
/// Compound files whose sector allocation table has several sectors, and whose table sectors are named by extra table sectors.
/// </summary>
public sealed class ContainerProbeChainTests
{
    private const int SectorSize = 512;
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint Free = 0xFFFFFFFF;

    private static void Entry(byte[] file, int sector, int index, string name, byte type, uint child)
    {
        int at = ((sector + 1) * SectorSize) + (index * 128);
        byte[] raw = Encoding.Unicode.GetBytes(name);
        raw.CopyTo(file, at);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(at + 0x40, 2), (ushort)(raw.Length + 2));
        file[at + 0x42] = type;
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at + 0x44, 4), Free);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at + 0x48, 4), Free);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at + 0x4C, 4), child);
    }

    // The directory is three sectors: the root in the first, a hop to a far sector, the stream in the last (entry 9); the table sector
    // that names the far sector's successor must be found.
    private static byte[] Build(int totalSectors, int secondDirectorySector, int thirdDirectorySector, bool cycle = false)
    {
        int tables = (totalSectors + 127) / 128;
        int extraTables = Math.Max(0, tables - 109);
        int extraSectors = (extraTables + 126) / 127;
        int firstDirectory = tables + extraSectors;
        byte[] file = new byte[(totalSectors + 1) * SectorSize];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(file, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(0x1E, 2), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x2C, 4), (uint)tables);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x30, 4), (uint)firstDirectory);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x44, 4), extraSectors > 0 ? (uint)tables : EndOfChain);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x48, 4), (uint)extraSectors);
        for (int i = 0; i < 109; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x4C + (i * 4), 4), i < tables ? (uint)i : Free);
        }

        for (int s = 0; s < extraSectors; s++)
        {
            int at = (tables + s + 1) * SectorSize;
            for (int e = 0; e < 127; e++)
            {
                int table = 109 + (s * 127) + e;
                BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at + (e * 4), 4), table < tables ? (uint)table : Free);
            }

            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at + (127 * 4), 4), s + 1 < extraSectors ? (uint)(tables + s + 1) : EndOfChain);
        }

        void Next(int sector, uint value)
        {
            int table = sector / 128;
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(((table + 1) * SectorSize) + ((sector % 128) * 4), 4), value);
        }

        Next(firstDirectory, (uint)secondDirectorySector);
        Next(secondDirectorySector, (uint)thirdDirectorySector);
        Next(thirdDirectorySector, cycle ? (uint)firstDirectory : EndOfChain);
        Entry(file, firstDirectory, 0, "Root Entry", 5, 9);
        Entry(file, thirdDirectorySector, 1, "WordDocument", 2, Free);
        return file;
    }

    private static async Task<string> Detect(byte[] bytes)
    {
        using MemoryStream stream = new(bytes);
        await using InputHandle input = new("test", stream, ownsInner: false);
        return (await FileTypeDetector.DetectAsync(input, CancellationToken.None)).Name;
    }

    [Fact]
    public async Task Detect_DirectoryContinuesInASectorOfTheSecondTableSector_IsFound()
    {
        Assert.Equal("DOC", await Detect(Build(400, 300, 200)));
    }

    [Fact]
    public async Task Detect_DirectoryContinuesBehindTheTablesOfTheHeader_IsFoundThroughTheExtraTableSectors()
    {
        Assert.Equal("DOC", await Detect(Build(14100, 14050, 500)));
    }

    [Fact]
    public async Task Detect_LoopedDirectoryChain_Ends()
    {
        Assert.Equal("DOC", await Detect(Build(400, 300, 200, cycle: true)));
    }
}
