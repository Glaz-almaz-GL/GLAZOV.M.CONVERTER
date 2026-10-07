using System.IO.Compression;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// The settings of the package: the defaults, the checks of values, the copy taken when the extension is created.
/// </summary>
public sealed class SettingsTests
{
    [Fact]
    public void Defaults_AreTheOnesTheGuideNames()
    {
        ArchivesSettings settings = new();

        Assert.Equal(100_000, settings.MaxEntries);
        Assert.Equal(10L * 1024 * 1024 * 1024, settings.MaxTotalBytes);
        Assert.Equal(4L * 1024 * 1024 * 1024, settings.MaxEntryBytes);
        Assert.Equal(500, settings.MaxCompressionRatio);
        Assert.Equal(CompressionLevel.Optimal, settings.CompressionLevel);
        Assert.Equal(437, settings.ZipNameCodePage);
    }

    [Fact]
    public void WrongValues_AreRefusedAtOnce()
    {
        ArchivesSettings settings = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxEntries = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxTotalBytes = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxEntryBytes = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxCompressionRatio = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ZipNameCodePage = 99999);
        Assert.Equal(437, settings.ZipNameCodePage);
    }

    [Fact]
    public async Task TheExtension_KeepsACopy_LaterChangesDoNotReachIt()
    {
        ArchivesSettings settings = new() { MaxEntries = 100 };
        Converter converter = ArchiveKit.With(settings);
        settings.MaxEntries = 1;

        byte[] tar = await ArchiveKit.ConvertAsync(converter, ArchiveKit.Make(FileFormat.Zip, ArchiveKit.Multi()), FileFormat.Tar);

        Assert.Equal(ArchiveKit.Multi().Count, ArchiveKit.Read(FileFormat.Tar, tar).Count);
    }

    [Fact]
    public async Task CompressionLevel_ChangesTheSizeOfTheResult()
    {
        byte[] source = ArchiveKit.Make(FileFormat.Tar, [new ArchiveSnapshot("t.txt", false, System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("compressible text ", 20_000))), ArchiveKit.Stamp)]);

        byte[] none = await ArchiveKit.ConvertAsync(ArchiveKit.With(new ArchivesSettings { CompressionLevel = CompressionLevel.NoCompression }), source, FileFormat.Zip);
        byte[] best = await ArchiveKit.ConvertAsync(ArchiveKit.With(new ArchivesSettings { CompressionLevel = CompressionLevel.SmallestSize }), source, FileFormat.Zip);

        Assert.True(best.Length * 4 < none.Length, $"none {none.Length}, smallest {best.Length}");
    }
}
