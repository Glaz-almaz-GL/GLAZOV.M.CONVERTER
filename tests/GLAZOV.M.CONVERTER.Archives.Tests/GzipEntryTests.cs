using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using System.Text;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// GZip holds one file: how its entry is named going into ZIP and TAR, and what happens to an archive that does not fit into it.
/// </summary>
public sealed class GzipEntryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-gz-" + Guid.NewGuid().ToString("N"));

    public GzipEntryTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task GzipFile_IsNamedLikeItselfWithoutDotGz_AndAStreamIsNamedData()
    {
        byte[] gzip = ArchiveKit.Make(FileFormat.Gzip, ArchiveKit.Single());
        string path = Path.Combine(_folder, "report.txt.gz");
        await File.WriteAllBytesAsync(path, gzip);
        string zipPath = Path.Combine(_folder, "out.zip");

        await ArchiveKit.With().ConvertAsync(Source.File(path), Target.File(zipPath, FileFormat.Zip));
        byte[] fromStream = await ArchiveKit.ConvertAsync(ArchiveKit.With(), gzip, FileFormat.Zip);

        Assert.Equal("report.txt", ArchiveKit.Read(FileFormat.Zip, await File.ReadAllBytesAsync(zipPath)).Single().Name);
        Assert.Equal("data", ArchiveKit.Read(FileFormat.Zip, fromStream).Single().Name);
    }

    [Fact]
    public async Task ZipWithOneFile_BecomesAGzipOfThatFile()
    {
        byte[] zip = ArchiveKit.Make(FileFormat.Zip, ArchiveKit.Single());

        byte[] gzip = await ArchiveKit.ConvertAsync(ArchiveKit.With(), zip, FileFormat.Gzip);

        Assert.Equal(ArchiveKit.Single()[0].Data, ArchiveKit.Read(FileFormat.Gzip, gzip).Single().Data);
    }

    [Fact]
    public async Task ArchiveWithSeveralEntries_DoesNotFitIntoGzip_AndLeavesNoFile()
    {
        string input = Path.Combine(_folder, "many.zip");
        string output = Path.Combine(_folder, "many.gz");
        await File.WriteAllBytesAsync(input, ArchiveKit.Make(FileFormat.Zip, ArchiveKit.Multi()));

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(async () =>
            await ArchiveKit.With().ConvertAsync(Source.File(input), Target.File(output, FileFormat.Gzip)));

        Assert.Equal(ConversionFailureReason.EngineFailed, error.Reason);
        Assert.Contains("одна запись", error.Message);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task EmptyArchive_DoesNotFitIntoGzip_AndAFolderOnlyArchiveNeither()
    {
        byte[] empty = ArchiveKit.Make(FileFormat.Zip, []);
        byte[] folderOnly = ArchiveKit.Make(FileFormat.Zip, [new ArchiveSnapshot("dir/", true, [], ArchiveKit.Stamp)]);

        foreach (byte[] source in new[] { empty, folderOnly })
        {
            using MemoryStream input = new(source);
            ConversionException error = await Assert.ThrowsAsync<ConversionException>(async () =>
                await ArchiveKit.With().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Gzip)));
            Assert.Contains("GZip", error.Message);
        }
    }

    [Fact]
    public async Task EmptyArchive_GoesIntoTarAndZip_AsAnEmptyArchive()
    {
        byte[] empty = ArchiveKit.Make(FileFormat.Zip, []);

        byte[] tar = await ArchiveKit.ConvertAsync(ArchiveKit.With(), empty, FileFormat.Tar);
        byte[] targz = await ArchiveKit.ConvertAsync(ArchiveKit.With(), empty, FileFormat.TarGz);

        Assert.Empty(ArchiveKit.Read(FileFormat.Tar, tar));
        Assert.Empty(ArchiveKit.Read(FileFormat.TarGz, targz));
    }

    [Fact]
    public async Task ZipNames_FollowTheCodePageOfTheSettings_WhenNotMarkedAsUtf8()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using MemoryStream stream = new();
        using (System.IO.Compression.ZipArchive zip = new(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true, Encoding.GetEncoding(866)))
        {
            using StreamWriter writer = new(zip.CreateEntry("Привет.txt").Open());
            writer.Write("x");
        }

        byte[] source = stream.ToArray();
        byte[] plain = await ArchiveKit.ConvertAsync(ArchiveKit.With(new ArchivesSettings { ZipNameCodePage = 866 }), source, FileFormat.Tar);
        byte[] wrong = await ArchiveKit.ConvertAsync(ArchiveKit.With(), source, FileFormat.Tar);

        Assert.Equal("Привет.txt", ArchiveKit.Read(FileFormat.Tar, plain).Single().Name);
        Assert.NotEqual("Привет.txt", ArchiveKit.Read(FileFormat.Tar, wrong).Single().Name);
    }
}
