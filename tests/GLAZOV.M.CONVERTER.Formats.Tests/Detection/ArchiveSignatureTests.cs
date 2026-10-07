using System.IO.Compression;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats.Detection;
using GLAZOV.M.CONVERTER.Formats.Detection.Archives;

namespace GLAZOV.M.CONVERTER.Formats.Tests.Detection;

/// <summary>
/// ZIP, TAR, TAR.GZ and GZip by content: real files, the packages of documents that are ZIP files and must keep their own names,
/// compressed SVG, cut and spoiled inputs, and the second look at a ZIP whose start does not settle it.
/// </summary>
public sealed class ArchiveSignatureTests
{
    private static byte[] Sample(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));
    }

    private static byte[] Gzipped(byte[] data)
    {
        using MemoryStream packed = new();
        using (GZipStream gzip = new(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return packed.ToArray();
    }

    [Theory]
    [InlineData("sample.zip", "ZIP")]
    [InlineData("sample-empty.zip", "ZIP")]
    [InlineData("sample.jar", "ZIP")]
    [InlineData("sample.epub", "ZIP")]
    [InlineData("sample.tar", "TAR")]
    [InlineData("sample.tar.gz", "TAR.GZ")]
    [InlineData("sample.gz", "GZip")]
    public void RealFile_IsNamedByItsContent(string file, string expected)
    {
        byte[] bytes = Sample(file);

        FileFormat type = Sniff.Match(bytes.AsSpan(0, Math.Min(bytes.Length, DetectionSizes.MinimumWindow)));

        Assert.Equal(expected, type.Name);
    }

    [Theory]
    [InlineData("sample.docx")]
    [InlineData("sample.xlsx")]
    [InlineData("sample.pptx")]
    [InlineData("sample.odt")]
    [InlineData("sample.ods")]
    [InlineData("sample.odp")]
    [InlineData("sample-macro.odt")]
    public void PackageOfADocument_IsNotZip(string file)
    {
        byte[] bytes = Sample(file);
        byte[] head = bytes.AsSpan(0, Math.Min(bytes.Length, DetectionSizes.MinimumWindow)).ToArray();

        Assert.False(ZipSignature.Matches(head));
        Assert.NotEqual("ZIP", Sniff.Match(head).Name);
    }

    [Theory]
    [InlineData("sample.zip", 3)]
    [InlineData("sample.zip", 29)]
    [InlineData("sample-empty.zip", 21)]
    [InlineData("sample.tar", 511)]
    [InlineData("sample.tar.gz", 9)]
    [InlineData("sample.gz", 9)]
    public void CutInput_IsUnknown_AndDoesNotThrow(string file, int keep)
    {
        Assert.Same(FileFormat.Unknown, Sniff.Match(Sample(file).AsSpan(0, keep)));
    }

    [Fact]
    public void SpoiledSigns_AreNotRecognized()
    {
        byte[] ustar = Sample("sample.tar");
        ustar[257] ^= 0x55;
        byte[] checksum = Sample("sample.tar");
        checksum[148] = (byte)'9';
        byte[] sum = Sample("sample.tar");
        sum[0] ^= 0x01;
        byte[] gzip = Sample("sample.gz");
        gzip[0] ^= 0x55;
        byte[] zip = Sample("sample.zip");
        zip[1] ^= 0x55;

        Assert.Same(FileFormat.Unknown, Sniff.Match(ustar));
        Assert.Same(FileFormat.Unknown, Sniff.Match(checksum));
        Assert.Same(FileFormat.Unknown, Sniff.Match(sum));
        Assert.Same(FileFormat.Unknown, Sniff.Match(gzip));
        Assert.Same(FileFormat.Unknown, Sniff.Match(zip));
    }

    [Fact]
    public void GzipOfATarWithASpoiledHeader_IsGzip_NotTarGz()
    {
        byte[] tar = Sample("sample.tar");
        tar[257] ^= 0x55;

        Assert.Equal("GZip", Sniff.Match(Gzipped(tar)).Name);
    }

    [Fact]
    public void CompressedSvg_IsSvg_NotGzip()
    {
        byte[] svgz = Gzipped(Sample("sample-plain.svg"));

        Assert.Equal("SVG", Sniff.Match(svgz).Name);
    }

    [Fact]
    public void NoSampleOfAnotherFormat_IsAnArchive()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Samples");
        string[] archives = ["sample.zip", "sample-empty.zip", "sample.jar", "sample.epub", "sample.tar", "sample.tar.gz", "sample.gz"];
        foreach (string path in Directory.EnumerateFiles(folder).Where(path => !archives.Contains(Path.GetFileName(path))))
        {
            byte[] bytes = File.ReadAllBytes(path);
            byte[] head = bytes.AsSpan(0, Math.Min(bytes.Length, DetectionSizes.MinimumWindow)).ToArray();

            Assert.True(
                !TarGzSignature.Matches(head) && !GzipSignature.Matches(head) && !ZipSignature.Matches(head) && !TarSignature.Matches(head),
                $"{Path.GetFileName(path)} is taken for an archive");
        }
    }

    // A ZIP made by the standard library; the first entry's name and size are the knobs that push the proof beyond the window.
    private static byte[] BuildZip(params (string Name, int Size)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, int size) in entries)
            {
                ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                using Stream content = entry.Open();
                byte[] data = new byte[size];
                new Random(7).NextBytes(data);
                content.Write(data);
            }
        }

        return stream.ToArray();
    }

    [Fact]
    public void SmallPackageThatLiesWhollyInTheWindow_IsItsDocument_NeverZip()
    {
        byte[] docx = BuildZip(("[Content_Types].xml", 40), ("word/document.xml", 40));
        byte[] folderFirst = BuildZip(("word/document.xml", 40), ("[Content_Types].xml", 40));
        byte[] noNamesFirst = BuildZip(("docProps/app.xml", 40), ("[Content_Types].xml", 40), ("xl/workbook.xml", 40));

        Assert.Equal("DOCX", Sniff.Match(docx).Name);
        Assert.False(ZipSignature.Matches(docx));
        Assert.False(ZipSignature.Matches(folderFirst));
        Assert.False(ZipSignature.Matches(noNamesFirst));
        Assert.Equal("XLSX", Sniff.Match(noNamesFirst).Name);
    }

    [Fact]
    public async Task ZipThatStartsLikeAPackage_IsLeftToTheSecondLook_AndIsZipWhenNoDocumentFits()
    {
        byte[] zip = BuildZip(("docProps/app.xml", 10), ("data.bin", 10));
        using MemoryStream stream = new(zip);

        FileFormat byLabel = Sniff.Match(zip);
        FileFormat full = await FormatsDetect.DetectAsync(Source.Stream(stream));

        Assert.Same(FileFormat.Unknown, byLabel);
        Assert.Equal("ZIP", full.Name);
    }

    [Fact]
    public async Task OfficePackageWhoseProofLiesBeyondTheWindow_IsADocument_NeverZip()
    {
        byte[] xlsx = BuildZip(("docProps/app.xml", 9000), ("[Content_Types].xml", 40), ("xl/workbook.xml", 40));
        using MemoryStream stream = new(xlsx);

        FileFormat byLabel = Sniff.Match(xlsx.AsSpan(0, DetectionSizes.MinimumWindow));
        FileFormat full = await FormatsDetect.DetectAsync(Source.Stream(stream));

        Assert.NotEqual("ZIP", byLabel.Name);
        Assert.Equal("XLSX", full.Name);
    }

    [Fact]
    public async Task FileOfEachArchive_IsDetectedThroughTheConverter()
    {
        foreach ((string file, string expected) in new[] { ("sample.zip", "ZIP"), ("sample.tar", "TAR"), ("sample.tar.gz", "TAR.GZ"), ("sample.gz", "GZip") })
        {
            using MemoryStream stream = new(Sample(file));

            Assert.Equal(expected, (await FormatsDetect.DetectAsync(Source.Stream(stream))).Name);
        }
    }
}
