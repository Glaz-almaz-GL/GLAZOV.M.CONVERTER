using DetectorChain = GLAZOV.M.CONVERTER.Detection.DetectorChain;
using System.IO.Compression;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats.Detection;
using GLAZOV.M.CONVERTER.Formats.Detection.Documents;
using GLAZOV.M.CONVERTER.IO;

namespace GLAZOV.M.CONVERTER.Formats.Tests.Detection;

/// <summary>
/// Second look at document containers: the central directory of an archive and the directory chain of a compound file are read
/// where they lie, however far from the start of the input.
/// </summary>
public sealed class ContainerProbeTests
{
    private static byte[] Sample(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));
    }

    private static async Task<FileFormat> Detect(byte[] bytes, long limit = 16L * 1024 * 1024)
    {
        using MemoryStream stream = new(bytes);
        await using InputHandle input = new("test", stream, ownsInner: false);
        return await FormatsDetect.DetectAsync(input, new DetectorChain(TestDetectors.Create(), inspectBudget: limit));
    }

    private static byte[] BigPackage(params (string Name, byte[] Content)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // A big stored part first: the leading bytes of the file never reach the entries that name the package.
            ZipArchiveEntry filler = zip.CreateEntry("media/big.bin", CompressionLevel.NoCompression);
            using (Stream filling = filler.Open())
            {
                byte[] noise = new byte[300 * 1024];
                new Random(7).NextBytes(noise);
                filling.Write(noise);
            }

            foreach ((string name, byte[] content) in entries)
            {
                using Stream entry = zip.CreateEntry(name).Open();
                entry.Write(content);
            }
        }

        return stream.ToArray();
    }

    [Theory]
    [InlineData("sample.doc", "DOC")]
    [InlineData("sample.xls", "XLS")]
    [InlineData("sample.ppt", "PPT")]
    [InlineData("sample.docx", "DOCX")]
    [InlineData("sample.xlsx", "XLSX")]
    [InlineData("sample.pptx", "PPTX")]
    [InlineData("sample.odt", "ODT")]
    public void Match_RealSamples_AreNamed(string file, string name)
    {
        using MemoryStream stream = new(Sample(file));

        Assert.Equal(name, ContainerProbe.Match(stream)?.Name);
    }

    [Fact]
    public async Task Detect_LargeOldPresentation_IsFoundWithTheDefaultWindow()
    {
        byte[] ppt = Sample("sample.ppt");

        // The directory of this file is in its last sector: the leading bytes alone do not reach it.
        Assert.Same(FileFormat.Unknown, Sniff.Match(ppt.AsSpan(0, Math.Min(ppt.Length, 64 * 1024))));
        Assert.Equal("PPT", (await Detect(ppt)).Name);
    }

    [Fact]
    public async Task Detect_PackageWhoseNamingEntriesLieBeyondTheWindow_IsFoundByItsCentralDirectory()
    {
        byte[] docx = BigPackage(("[Content_Types].xml", "<Types/>"u8.ToArray()), ("word/document.xml", "<w:document/>"u8.ToArray()));
        byte[] xlsx = BigPackage(("[Content_Types].xml", "<Types/>"u8.ToArray()), ("xl/workbook.xml", "<workbook/>"u8.ToArray()));

        Assert.Same(FileFormat.Unknown, Sniff.Match(docx.AsSpan(0, 64 * 1024)));
        Assert.Equal("DOCX", (await Detect(docx)).Name);
        Assert.Equal("XLSX", (await Detect(xlsx)).Name);
    }

    [Fact]
    public async Task Detect_PlainArchive_IsNotADocument()
    {
        byte[] plain = BigPackage(("notes.txt", "x"u8.ToArray()));
        byte[] folderOnly = BigPackage(("word/notes.txt", "x"u8.ToArray()));

        Assert.Same(FileFormat.Unknown, await Detect(plain));
        Assert.Same(FileFormat.Unknown, await Detect(folderOnly));
    }

    [Theory]
    [InlineData("sample.ppt")]
    [InlineData("sample.xls")]
    public async Task Detect_CutFile_IsUnknown_AndNeverThrows(string file)
    {
        byte[] bytes = Sample(file);

        foreach (int length in new[] { 0, 7, 100, 511, 512, 600, bytes.Length / 2 })
        {
            Assert.Same(FileFormat.Unknown, await Detect(bytes.AsSpan(0, length).ToArray()));
        }
    }

    [Fact]
    public async Task Detect_BigPackageCutInHalf_IsUnknown()
    {
        byte[] docx = BigPackage(("[Content_Types].xml", "<Types/>"u8.ToArray()), ("word/document.xml", "<w:document/>"u8.ToArray()));

        Assert.Same(FileFormat.Unknown, await Detect(docx.AsSpan(0, docx.Length / 2).ToArray()));
    }

    [Fact]
    public async Task Detect_ReadsOnlyUpToTheLimit()
    {
        byte[] ppt = Sample("sample.ppt");

        Assert.Equal("PPT", (await Detect(ppt, 16 * 1024)).Name);
        Assert.Same(FileFormat.Unknown, await Detect(ppt, 1024));
    }

    [Fact]
    public async Task Detect_PutsTheStreamBackWhereItStarted()
    {
        byte[] docx = BigPackage(("[Content_Types].xml", "<Types/>"u8.ToArray()), ("word/document.xml", "<w:document/>"u8.ToArray()));
        byte[] padded = [.. new byte[10], .. docx];
        using MemoryStream stream = new(padded) { Position = 10 };
        await using InputHandle input = new("test", stream, ownsInner: false);

        FileFormat found = await FormatsDetect.DetectAsync(input, CancellationToken.None);

        Assert.Equal("DOCX", found.Name);
        Assert.Equal(10, stream.Position);
    }

    [Fact]
    public async Task Detect_StreamThatCannotSeek_KeepsTheAnswerOfTheLeadingBytes()
    {
        byte[] ppt = Sample("sample.ppt");
        await using InputHandle input = new("test", new ForwardReadStream(ppt), ownsInner: true);

        Assert.False(input.CanProbe);
        Assert.Same(FileFormat.Unknown, await FormatsDetect.DetectAsync(input, CancellationToken.None));
    }
}
