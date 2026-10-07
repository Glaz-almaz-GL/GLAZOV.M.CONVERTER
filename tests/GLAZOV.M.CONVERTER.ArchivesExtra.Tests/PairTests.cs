using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Tests;

/// <summary>
/// The pairs of the package on real files made by other programs (7z by bsdtar, RAR by hand, XZ and BZip2 by Python): names, folders, content.
/// </summary>
public sealed class PairTests
{
    private static readonly FileFormat[] Targets =
    [
        FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz, ArchivesExtraFormats.Bzip2, ArchivesExtraFormats.TarBz2, FileFormat.Gzip,
    ];

    public static TheoryData<string, string> ManyEntrySamples()
    {
        TheoryData<string, string> data = [];
        foreach (string sample in new[] { "sample.7z", "sample-lzma2.7z", "sample.rar", "sample.tar.xz", "sample.tar.bz2" })
        {
            foreach (FileFormat target in Targets.Where(t => !t.Equals(FileFormat.Gzip) && !t.Equals(ArchivesExtraFormats.Bzip2)))
            {
                data.Add(sample, target.Name);
            }
        }

        return data;
    }

    public static TheoryData<string, string> SingleFileSamples()
    {
        TheoryData<string, string> data = [];
        foreach (string sample in new[] { "sample.xz", "sample.bz2" })
        {
            foreach (FileFormat target in Targets)
            {
                data.Add(sample, target.Name);
            }
        }

        return data;
    }

    private static FileFormat Kind(string name)
    {
        return Targets.Single(t => t.Name == name);
    }

    [Theory]
    [MemberData(nameof(ManyEntrySamples))]
    public async Task ArchiveWithSeveralEntries_MovesTheEntriesWithNamesAndContent(string sample, string targetName)
    {
        FileFormat to = Kind(targetName);
        bool russian = !sample.EndsWith(".rar", StringComparison.Ordinal);
        string path = ExtraKit.Sample(sample);

        ConversionResult result = await ExtraKit.With().ConvertAsync(Source.File(path), Target.Memory(to));

        List<(string Name, bool IsDirectory, byte[] Data)> back = ExtraKit.Read(to, result.Bytes!);
        string[] names = ExtraKit.Names(russian);
        // The 7z written by bsdtar lists the files first and the folder last; every other sample lists them in the order of the names.
        Assert.Equal(names.Order(StringComparer.Ordinal), back.Select(e => e.Name).Order(StringComparer.Ordinal));
        foreach ((string name, bool isDirectory, byte[] data) in back)
        {
            Assert.Equal(name.EndsWith('/'), isDirectory);
            Assert.Equal(name switch
            {
                "empty.bin" => [],
                "data.bin" => ExtraKit.Pattern(),
                var n when n.EndsWith(".txt", StringComparison.Ordinal) => ExtraKit.Hello(),
                _ => (byte[])[],
            }, data);
        }
    }

    [Theory]
    [MemberData(nameof(SingleFileSamples))]
    public async Task SingleCompressedFile_BecomesTheOneEntry_NamedLikeItsInput_AndGoesIntoGzipAndBzip2Too(string sample, string targetName)
    {
        FileFormat to = Kind(targetName);
        string path = ExtraKit.Sample(sample);
        byte[] expected = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(System.Text.Encoding.UTF8.GetString(ExtraKit.Hello()), 20)));

        ConversionResult result = await ExtraKit.With().ConvertAsync(Source.File(path), Target.Memory(to));

        List<(string Name, bool IsDirectory, byte[] Data)> back = ExtraKit.Read(to, result.Bytes!);
        (string name, bool _, byte[] data) = Assert.Single(back);
        Assert.Equal(expected, data);
        if (!to.Equals(FileFormat.Gzip) && !to.Equals(ArchivesExtraFormats.Bzip2))
        {
            Assert.Equal("sample", name);
        }
    }

    [Fact]
    public async Task BaseKindsGoIntoBzip2AndTarBz2_AndBack()
    {
        Converter converter = ExtraKit.With();
        string zip = Path.Combine(Path.GetTempPath(), "extra-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await File.WriteAllBytesAsync(zip, ExtraKit.Bytes("sample.rar").Length > 0 ? (await converter.ConvertAsync(Source.File(ExtraKit.Sample("sample.rar")), Target.Memory(FileFormat.Zip))).Bytes! : []);

            ConversionResult tarBz2 = await converter.ConvertAsync(Source.File(zip), Target.Memory(ArchivesExtraFormats.TarBz2));
            ConversionResult again = await converter.ConvertAsync(Source.Stream(new MemoryStream(tarBz2.Bytes!)), Target.Memory(FileFormat.Zip));

            Assert.Equal(ExtraKit.Names(false), ExtraKit.Read(FileFormat.Zip, again.Bytes!).Select(e => e.Name));
        }
        finally
        {
            File.Delete(zip);
        }
    }

    [Fact]
    public void Catalog_HoldsTheFortyFourPairs_NoneWritesSevenZipRarOrXz_AndBaseToBaseIsLeftToTheBasePackage()
    {
        ConverterCapabilities caps = ExtraKit.With().Capabilities;

        Assert.Equal(44, caps.All.Count);
        Assert.All(caps.All, pair => Assert.Equal(ConversionAvailability.Ready, pair.Availability));
        Assert.False(caps.CanConvert(FileFormat.Zip, ArchivesExtraFormats.SevenZip));
        Assert.False(caps.CanConvert(ArchivesExtraFormats.SevenZip, ArchivesExtraFormats.Rar));
        Assert.False(caps.CanConvert(FileFormat.Tar, ArchivesExtraFormats.Xz));
        Assert.False(caps.CanConvert(FileFormat.Tar, ArchivesExtraFormats.TarXz));
        Assert.False(caps.CanConvert(FileFormat.Zip, FileFormat.Tar));
        Assert.True(caps.CanConvert(ArchivesExtraFormats.SevenZip, FileFormat.Zip));
        Assert.True(caps.CanConvert(FileFormat.Zip, ArchivesExtraFormats.TarBz2));
    }

    [Fact]
    public void BothPackagesTogether_GiveSixteenPlusFortyFourPairs_WithoutAClash()
    {
        ConverterCapabilities caps = ExtraKit.With(withBase: true).Capabilities;

        Assert.Equal(60, caps.All.Count);
        Assert.True(caps.CanConvert(FileFormat.Zip, FileFormat.Tar));
        Assert.True(caps.CanConvert(ArchivesExtraFormats.Rar, FileFormat.TarGz));
    }

    [Fact]
    public async Task SevenZip_WrittenAsAResult_IsRefusedAtOnce_NotInTheMiddleOfTheWork()
    {
        Errors.ConversionException error = await Assert.ThrowsAsync<Errors.ConversionException>(async () =>
            await ExtraKit.With().ConvertAsync(Source.File(ExtraKit.Sample("sample.rar")), Target.Memory(ArchivesExtraFormats.SevenZip)));

        Assert.Equal(Errors.ConversionFailureReason.UnsupportedConversion, error.Reason);
    }
}
