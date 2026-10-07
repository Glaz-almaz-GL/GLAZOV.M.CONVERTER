using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.ArchivesExtra.Detection;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Tests;

/// <summary>
/// 7z, RAR, XZ, TAR.XZ, BZip2 and TAR.BZ2 by their signs: real files, files and streams that cannot seek, cut and spoiled inputs, no overlap with other formats.
/// </summary>
public sealed class DetectionTests
{
    [Theory]
    [InlineData("sample.7z", "7z")]
    [InlineData("sample-lzma2.7z", "7z")]
    [InlineData("sample.rar", "RAR")]
    [InlineData("sample.xz", "XZ")]
    [InlineData("sample.tar.xz", "TAR.XZ")]
    [InlineData("sample.bz2", "BZip2")]
    [InlineData("sample.tar.bz2", "TAR.BZ2")]
    public async Task RealFile_IsNamedByItsContent_AsAFile_AndByTheLabelsAlone(string file, string expected)
    {
        Converter converter = ExtraKit.With();
        byte[] bytes = ExtraKit.Bytes(file);

        FileFormat byFile = await converter.DetectAsync(Source.File(ExtraKit.Sample(file)));
        // The labels need no seeking: the detector names the file from the window alone.
        FileFormat? byLabels = new ExtraArchiveDetector().Detect(bytes.AsSpan(0, Math.Min(bytes.Length, 4096)));

        Assert.Equal(expected, byFile.Name);
        Assert.Equal(expected, byLabels?.Name);
    }

    [Theory]
    [InlineData("sample.7z", 5)]
    [InlineData("sample.rar", 6)]
    [InlineData("sample.xz", 5)]
    [InlineData("sample.bz2", 9)]
    [InlineData("sample.tar.xz", 5)]
    [InlineData("sample.tar.bz2", 9)]
    public void CutInsideTheMagic_IsUnknown_AndDoesNotThrow(string file, int keep)
    {
        Assert.Null(new ExtraArchiveDetector().Detect(ExtraKit.Bytes(file).AsSpan(0, keep)));
    }

    [Fact]
    public void SpoiledSigns_AreNotRecognized()
    {
        byte[] sevenZip = ExtraKit.Bytes("sample.7z");
        sevenZip[2] ^= 0x55;
        byte[] rar = ExtraKit.Bytes("sample.rar");
        rar[6] = 0x09;
        byte[] xz = ExtraKit.Bytes("sample.xz");
        xz[0] ^= 0x55;
        byte[] bzip2Level = ExtraKit.Bytes("sample.bz2");
        bzip2Level[3] = (byte)'0';
        byte[] bzip2Block = ExtraKit.Bytes("sample.bz2");
        bzip2Block[5] ^= 0x55;

        ExtraArchiveDetector detector = new();

        Assert.Null(detector.Detect(sevenZip));
        Assert.Null(detector.Detect(rar));
        Assert.Null(detector.Detect(xz));
        Assert.Null(detector.Detect(bzip2Level));
        Assert.Null(detector.Detect(bzip2Block));
    }

    [Fact]
    public void XzOrBzip2_WhoseFirstBlockIsNotATarHeader_IsNotATarArchive()
    {
        Assert.Equal("XZ", new ExtraArchiveDetector().Detect(ExtraKit.Bytes("sample.xz"))!.Name);
        Assert.Equal("BZip2", new ExtraArchiveDetector().Detect(ExtraKit.Bytes("sample.bz2"))!.Name);
    }

    [Fact]
    public void EverySampleOfTheOtherPackages_IsNoneOfTheseKinds_AndTheseSamplesAreNoneOfTheStandardArchives()
    {
        ExtraArchiveDetector extra = new();
        // The samples of the other packages; the common set of the composition tests also carries copies of this package's own samples, which are left out by name.
        string[] own = [.. Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Samples")).Select(Path.GetFileName).OfType<string>()];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "FormatsSamples"))
                     .Concat(Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "CompositionSamples")))
                     .Where(path => !own.Contains(Path.GetFileName(path))))
        {
            byte[] bytes = File.ReadAllBytes(path);
            byte[] head = bytes.AsSpan(0, Math.Min(bytes.Length, 4096)).ToArray();

            Assert.True(extra.Detect(head) is null, $"{Path.GetFileName(path)} is taken for {extra.Detect(head)?.Name}");
        }

        foreach (string path in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Samples")))
        {
            byte[] bytes = File.ReadAllBytes(path);
            byte[] head = bytes.AsSpan(0, Math.Min(bytes.Length, 4096)).ToArray();

            Assert.True(StandardDetectors.Archives.Detect(head) is null, $"{Path.GetFileName(path)} is taken for {StandardDetectors.Archives.Detect(head)?.Name}");
        }
    }

    [Fact]
    public async Task BothPackagesPluggedInEitherOrder_NameEverythingTheSame()
    {
        string[] files = ["sample.7z", "sample.rar", "sample.xz", "sample.tar.xz", "sample.bz2", "sample.tar.bz2"];
        foreach (bool baseFirst in new[] { false, true })
        {
            ConverterSettings settings = new();
            settings.Extensions.Add(new FormatsExtension());
            if (baseFirst)
            {
                settings.Extensions.Add(new Archives.ArchivesExtension());
                settings.Extensions.Add(new ArchivesExtraExtension());
            }
            else
            {
                settings.Extensions.Add(new ArchivesExtraExtension());
                settings.Extensions.Add(new Archives.ArchivesExtension());
            }

            Converter converter = new(settings);
            foreach (string file in files)
            {
                Assert.NotEqual(FileFormat.Unknown, await converter.DetectAsync(Source.File(ExtraKit.Sample(file))));
            }

            Assert.Equal("ZIP", (await converter.DetectAsync(Source.File(Path.Combine(AppContext.BaseDirectory, "CompositionSamples", "sample-big.zip")))).Name);
            Assert.Equal("DOCX", (await converter.DetectAsync(Source.File(Path.Combine(AppContext.BaseDirectory, "CompositionSamples", "sample.docx")))).Name);
        }
    }
}
