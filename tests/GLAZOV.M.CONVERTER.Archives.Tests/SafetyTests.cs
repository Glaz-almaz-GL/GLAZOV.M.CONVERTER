using System.Formats.Tar;
using System.IO.Compression;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// What the engine refuses: dangerous names, encrypted entries, links, duplicates, archive bombs and every limit. Each refusal is an error that
/// names its cause, and no result file is left behind.
/// </summary>
public sealed class SafetyTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convtests-arch-" + Guid.NewGuid().ToString("N"));

    public SafetyTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static byte[] Zip(params (string Name, string Text)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string text) in entries)
            {
                using StreamWriter writer = new(zip.CreateEntry(name).Open());
                writer.Write(text);
            }
        }

        return stream.ToArray();
    }

    private static byte[] Tar(Action<TarWriter> write)
    {
        using MemoryStream stream = new();
        using (TarWriter tar = new(stream, TarEntryFormat.Pax, leaveOpen: true))
        {
            write(tar);
        }

        return stream.ToArray();
    }

    private async Task<ConversionException> RefusedAsync(Converter converter, byte[] source, FileFormat to)
    {
        string input = Path.Combine(_folder, "in" + Guid.NewGuid().ToString("N")[..6] + ".bin");
        string output = Path.Combine(_folder, "out" + Guid.NewGuid().ToString("N")[..6] + "." + to.Extension);
        await File.WriteAllBytesAsync(input, source);

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(Source.File(input), Target.File(output, to)));

        Assert.False(File.Exists(output), "a refused archive left a result file");
        Assert.Equal(ConversionFailureReason.EngineFailed, error.Reason);
        return error;
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a/../../evil.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\evil.txt")]
    [InlineData("..\\evil.txt")]
    public async Task DangerousNameInZip_IsRefused(string name)
    {
        ConversionException error = await RefusedAsync(ArchiveKit.With(), Zip(("fine.txt", "x"), (name, "x")), FileFormat.Tar);

        Assert.Contains("Опасная запись", error.Message);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("/abs.txt")]
    public async Task DangerousNameInTar_IsRefused(string name)
    {
        byte[] tar = Tar(writer => writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream([1, 2, 3]) }));

        ConversionException error = await RefusedAsync(ArchiveKit.With(), tar, FileFormat.Zip);

        Assert.Contains("Опасная запись", error.Message);
    }

    [Fact]
    public async Task EncryptedEntryInZip_IsRefused()
    {
        byte[] zip = Zip(("secret.txt", "text"));
        int central = zip.AsSpan().IndexOf(new byte[] { 0x50, 0x4B, 0x01, 0x02 });
        zip[central + 8] |= 0x01;
        zip[6] |= 0x01;

        ConversionException error = await RefusedAsync(ArchiveKit.With(), zip, FileFormat.Tar);

        Assert.Contains("зашифрована", error.Message);
    }

    [Theory]
    [InlineData(TarEntryType.SymbolicLink)]
    [InlineData(TarEntryType.HardLink)]
    public async Task LinkInTar_IsRefusedNotLost(TarEntryType type)
    {
        byte[] tar = Tar(writer =>
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "a.txt") { DataStream = new MemoryStream([1]) });
            writer.WriteEntry(new PaxTarEntry(type, "link") { LinkName = "a.txt" });
        });

        ConversionException error = await RefusedAsync(ArchiveKit.With(), tar, FileFormat.Zip);

        Assert.Contains("ссылка", error.Message);
    }

    [Fact]
    public async Task SymbolicLinkInZip_IsRefusedNotTurnedIntoAFile()
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry link = zip.CreateEntry("link");
            link.ExternalAttributes = 0xA1FF << 16;
            using StreamWriter writer = new(link.Open());
            writer.Write("target.txt");
        }

        ConversionException error = await RefusedAsync(ArchiveKit.With(), stream.ToArray(), FileFormat.Tar);

        Assert.Contains("ссылка", error.Message);
    }

    [Fact]
    public async Task SpecialFileInTar_IsRefused()
    {
        byte[] tar = Tar(writer => writer.WriteEntry(new PaxTarEntry(TarEntryType.Fifo, "pipe")));

        ConversionException error = await RefusedAsync(ArchiveKit.With(), tar, FileFormat.Zip);

        Assert.Contains("особый файл", error.Message);
    }

    [Fact]
    public async Task TwoEntriesWithOneName_AreRefused()
    {
        byte[] tar = Tar(writer =>
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "a.txt") { DataStream = new MemoryStream([1]) });
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "./a.txt") { DataStream = new MemoryStream([2]) });
        });

        ConversionException error = await RefusedAsync(ArchiveKit.With(), tar, FileFormat.Zip);

        Assert.Contains("одним именем", error.Message);
    }

    [Fact]
    public async Task ArchiveBomb_IsRefusedByTheRatio_AndTheMessageNamesTheSetting()
    {
        byte[] zeroes = new byte[40 * 1024 * 1024];
        byte[] gzip = ArchiveKit.Make(FileFormat.Gzip, [new ArchiveSnapshot("z", false, zeroes, ArchiveKit.Stamp)]);

        ConversionException error = await RefusedAsync(ArchiveKit.With(), gzip, FileFormat.Zip);

        Assert.Contains("архив-бомбу", error.Message);
        Assert.Contains("MaxCompressionRatio", error.Message);
    }

    [Fact]
    public async Task ABomb_IsAllowedWhenTheRatioIsRaised()
    {
        byte[] zeroes = new byte[8 * 1024 * 1024];
        byte[] gzip = ArchiveKit.Make(FileFormat.Gzip, [new ArchiveSnapshot("z", false, zeroes, ArchiveKit.Stamp)]);

        byte[] zip = await ArchiveKit.ConvertAsync(ArchiveKit.With(new ArchivesSettings { MaxCompressionRatio = 100_000 }), gzip, FileFormat.Zip);

        Assert.Equal(zeroes.Length, ArchiveKit.Read(FileFormat.Zip, zip).Single().Data.Length);
    }

    [Fact]
    public async Task TooManyEntries_AreRefused_NamingMaxEntries()
    {
        byte[] zip = Zip(("a", "1"), ("b", "2"), ("c", "3"));

        ConversionException error = await RefusedAsync(ArchiveKit.With(new ArchivesSettings { MaxEntries = 2 }), zip, FileFormat.Tar);

        Assert.Contains("MaxEntries", error.Message);
    }

    [Fact]
    public async Task TooLargeEntry_IsRefused_NamingMaxEntryBytes_AlreadyByItsDeclaredSize()
    {
        byte[] zip = Zip(("big.txt", new string('x', 5000)));

        ConversionException error = await RefusedAsync(ArchiveKit.With(new ArchivesSettings { MaxEntryBytes = 1000 }), zip, FileFormat.Tar);

        Assert.Contains("MaxEntryBytes", error.Message);
        Assert.Contains("big.txt", error.Message);
    }

    [Fact]
    public async Task TooLargeEntry_OfAGzip_WhoseSizeIsNotDeclared_IsRefusedWhileItIsRead()
    {
        byte[] gzip = ArchiveKit.Make(FileFormat.Gzip, [new ArchiveSnapshot("x", false, new byte[5000], ArchiveKit.Stamp)]);

        ConversionException error = await RefusedAsync(ArchiveKit.With(new ArchivesSettings { MaxEntryBytes = 1000 }), gzip, FileFormat.Zip);

        Assert.Contains("MaxEntryBytes", error.Message);
    }

    [Fact]
    public async Task TooLargeTotal_IsRefused_NamingMaxTotalBytes()
    {
        byte[] zip = Zip(("a", new string('a', 600)), ("b", new string('b', 600)));

        ConversionException error = await RefusedAsync(ArchiveKit.With(new ArchivesSettings { MaxTotalBytes = 1000 }), zip, FileFormat.Tar);

        Assert.Contains("MaxTotalBytes", error.Message);
    }

    [Fact]
    public async Task TruncatedTarGz_IsRefusedAsBroken_NotAsAHalfArchive()
    {
        byte[] whole = ArchiveKit.Make(FileFormat.TarGz, ArchiveKit.Multi());

        ConversionException error = await RefusedAsync(ArchiveKit.With(), whole.AsSpan(0, whole.Length / 2).ToArray(), FileFormat.Zip);

        Assert.Contains("оборван", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ZipCutOffAtTheEnd_IsNotConfirmedAsAZip_NeverHalfConverted()
    {
        byte[] zip = ArchiveKit.Make(FileFormat.Zip, ArchiveKit.Multi());
        string input = Path.Combine(_folder, "cut.zip");
        string output = Path.Combine(_folder, "cut.tar");
        await File.WriteAllBytesAsync(input, zip.AsSpan(0, zip.Length - 40).ToArray());

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(async () =>
            await ArchiveKit.With().ConvertAsync(Source.File(input), Target.File(output, FileFormat.Tar)));

        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, error.Reason);
        Assert.False(File.Exists(output));
    }
}
