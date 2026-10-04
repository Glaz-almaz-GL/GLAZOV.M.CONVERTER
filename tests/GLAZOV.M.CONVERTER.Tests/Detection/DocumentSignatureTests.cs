using System.IO.Compression;
using System.Text;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tests.Detection;

/// <summary>
/// Documents are recognized by their first bytes on real files made by LibreOffice (Samples folder): archives by the
/// entries inside the window, old Office files by the directory of the compound file when it is inside the window.
/// What is beyond the window is not guessed.
/// </summary>
public class DocumentSignatureTests
{
    private const int DefaultWindow = ConverterSettings.DefaultDetectionWindow;

    private static byte[] Sample(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));
    }

    private static FileFormat Match(byte[] bytes, int window = DefaultWindow)
    {
        return SignatureMatcher.Match(bytes.AsSpan(0, Math.Min(bytes.Length, window)), Math.Max(window, SignatureMatcher.HeadLength));
    }

    [Theory]
    [InlineData("sample.pdf", "PDF")]
    [InlineData("sample.docx", "DOCX")]
    [InlineData("sample.doc", "DOC")]
    [InlineData("sample.odt", "ODT")]
    [InlineData("sample.rtf", "RTF")]
    [InlineData("sample.xlsx", "XLSX")]
    [InlineData("sample.xls", "XLS")]
    [InlineData("sample.ods", "ODS")]
    [InlineData("sample.pptx", "PPTX")]
    [InlineData("sample.odp", "ODP")]
    [InlineData("sample.html", "HTML")]
    public void Match_RealDocument_IsConfirmedByItsBytes_AtTheDefaultWindow(string file, string expected)
    {
        Assert.Equal(expected, Match(Sample(file)).Name);
    }

    [Fact]
    public void Match_LargeOldPresentation_NeedsAWindowThatReachesItsDirectory()
    {
        byte[] ppt = Sample("sample.ppt");

        // The directory of this 450 KB file is in its last sector: the default window does not reach it.
        Assert.Same(FileFormat.Unknown, Match(ppt));
        Assert.Equal("PPT", Match(ppt, ppt.Length).Name);
    }

    [Fact]
    public void Match_OldFormats_AreToldApart()
    {
        Assert.Same(FileFormat.Doc, Match(Sample("sample.doc")));
        Assert.Same(FileFormat.Xls, Match(Sample("sample.xls")));
        Assert.Same(FileFormat.Ppt, Match(Sample("sample.ppt"), Sample("sample.ppt").Length));
    }

    [Theory]
    [InlineData("sample.docx")]
    [InlineData("sample.xlsx")]
    [InlineData("sample.pptx")]
    [InlineData("sample.odt")]
    [InlineData("sample.doc")]
    [InlineData("sample.xls")]
    public void Match_ArchiveOrCompoundFileCutShort_IsUnknown_AndNeverThrows(string file)
    {
        byte[] bytes = Sample(file);

        for (int length = 0; length <= 40; length++)
        {
            Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(bytes.AsSpan(0, length)));
        }

    }

    [Fact]
    public void Match_NameOfTheFileIsNotAnInput()
    {
        // The same bytes, whatever they are called: a DOCX is a DOCX with no name at all.
        Assert.Same(FileFormat.Docx, SignatureMatcher.Match(Sample("sample.docx")));
    }

    [Fact]
    public void Match_ZipWithoutOfficeEntries_IsNotADocument()
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string name in new[] { "readme.txt", "data/values.csv", "wordy/document.xml", "excel/sheet.xml", "pptx/a.xml" })
            {
                using StreamWriter writer = new(zip.CreateEntry(name).Open());
                writer.Write("текст");
            }
        }

        Assert.Same(FileFormat.Unknown, Match(stream.ToArray()));
    }

    [Fact]
    public void Match_ZipWithAWordMainPart_IsDocx_AndOneWithAnotherMimetypeIsNot()
    {
        Assert.Same(FileFormat.Docx, Match(Zip(("[Content_Types].xml", "<Types/>"), ("word/document.xml", "<w:document/>"))));
        Assert.Same(FileFormat.Xlsx, Match(Zip(("[Content_Types].xml", "<Types/>"), ("xl/workbook.xml", "<workbook/>"))));
        Assert.Same(FileFormat.Pptx, Match(Zip(("[Content_Types].xml", "<Types/>"), ("ppt/presentation.xml", "<p/>"))));
        Assert.Same(FileFormat.Unknown, Match(Zip(("mimetype", "application/epub+zip"), ("META-INF/container.xml", "<c/>"))));
        Assert.Same(FileFormat.Odt, Match(Zip(("mimetype", "application/vnd.oasis.opendocument.text"), ("content.xml", "<c/>"))));
    }

    [Fact]
    public void Match_CompoundFileWithUnknownStreams_IsNotGuessed()
    {
        byte[] xls = Sample("sample.xls");
        byte[] renamed = (byte[])xls.Clone();
        byte[] workbook = Encoding.Unicode.GetBytes("Workbook");
        byte[] other = Encoding.Unicode.GetBytes("Notebook");
        int at = IndexOf(renamed, workbook);
        Assert.True(at > 0);
        other.CopyTo(renamed, at);

        Assert.Same(FileFormat.Unknown, Match(renamed));
    }

    [Fact]
    public void Match_CompoundFile_WhoseWorkbookIsAStorageNotAStream_IsNotXls()
    {
        byte[] xls = (byte[])Sample("sample.xls").Clone();
        int at = IndexOf(xls, Encoding.Unicode.GetBytes("Workbook"));
        Assert.True(at > 0);
        xls[at + 0x42] = 1; // object type: storage

        Assert.Same(FileFormat.Unknown, Match(xls));
    }

    [Theory]
    [InlineData("<!DOCTYPE html><html><body>x</body></html>")]
    [InlineData("<!doctype HTML>\n<html>")]
    [InlineData("  \r\n\t<HTML lang=\"ru\"><HEAD>")]
    [InlineData("<head><title>t</title></head>")]
    [InlineData("<body>просто</body>")]
    public void Match_HtmlStart_IsHtml_AfterSpacesAndByteOrderMark(string text)
    {
        Assert.Same(FileFormat.Html, SignatureMatcher.Match(Encoding.UTF8.GetBytes(text)));
        Assert.Same(FileFormat.Html, SignatureMatcher.Match([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)]));
    }

    [Theory]
    [InlineData("Это письмо про тег <html> и ничего больше.")]
    [InlineData("%PD")]
    [InlineData("{\\rt")]
    [InlineData("<svgfoo></svgfoo>")]
    [InlineData("PK")]
    public void Match_TextThatMerelyMentionsOrHalfStartsAFormat_IsNotADocument(string text)
    {
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void Match_OlderTypesAreNotStolen()
    {
        Assert.Same(FileFormat.M4a, SignatureMatcher.Match(Sample("sample.m4a")));
        Assert.Same(FileFormat.Wav, SignatureMatcher.Match(Sample("sample.wav")));
        Assert.Same(FileFormat.Svg, SignatureMatcher.Match(Sample("sample-plain.svg")));
        Assert.Same(FileFormat.Png, SignatureMatcher.Match(Sample("sample.png")));
        Assert.Same(FileFormat.Mp4, SignatureMatcher.Match(Sample("sample.mp4")));
    }

    [Fact]
    public void Review_PlainZipWithAFolderNamedLikeAnOfficeOne_IsNotADocument()
    {
        Assert.Same(FileFormat.Unknown, Match(Zip(("word/notes.txt", "x"))));
        Assert.Same(FileFormat.Unknown, Match(Zip(("xl/data.csv", "x"))));
        Assert.Same(FileFormat.Unknown, Match(Zip(("ppt/talk.txt", "x"))));
    }

    [Theory]
    [InlineData("<header>x</header>")]
    [InlineData("<headline>x</headline>")]
    [InlineData("<bodyguard/>")]
    [InlineData("<htmlfoo/>")]
    public void Review_XmlWhoseFirstTagOnlyStartsLikeAnHtmlTag_IsNotHtml(string text)
    {
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void Review_WordFileThatAlsoHoldsAWorkbookStreamEarlier_IsStillDoc()
    {
        // A DOC with an embedded sheet lists a "Workbook" entry besides "WordDocument"; the main stream decides.
        byte[] doc = (byte[])Sample("sample.doc").Clone();
        byte[] summary = Encoding.Unicode.GetBytes("\u0005SummaryInformation");
        int at = IndexOf(doc, summary);
        Assert.True(at > 0);
        byte[] workbook = Encoding.Unicode.GetBytes("Workbook\0");
        Array.Clear(doc, at, 64);
        workbook.CopyTo(doc, at);
        BitConverter.GetBytes((ushort)workbook.Length).CopyTo(doc, at + 0x40);

        Assert.Same(FileFormat.Doc, Match(doc));
    }

    [Theory]
    [InlineData("sample.docx")]
    [InlineData("sample.xlsx")]
    [InlineData("sample.pptx")]
    public void Review_PlainZipThatStoresAnOfficeFileInside_IsNotThatDocument(string inner)
    {
        // A plain archive of a stored (uncompressed) document: the inner local headers lie in the window but belong to another file.
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry("report" + Path.GetExtension(inner), CompressionLevel.NoCompression);
            using Stream entryStream = entry.Open();
            entryStream.Write(Sample(inner));
        }

        Assert.Same(FileFormat.Unknown, Match(stream.ToArray(), 1 << 20));
    }

    [Theory]
    [InlineData("sample.docx")]
    [InlineData("sample.xlsx")]
    [InlineData("sample.pptx")]
    public void Review_StreamedZipThatStoresAnOfficeFileInside_IsNotThatDocument(string inner)
    {
        // The outer archive is written to a stream that cannot seek: its entry gets a data descriptor (sizes hidden in the header),
        // and the stored inner archive must still not be read as part of the outer one.
        using MemoryStream target = new();
        using (ForwardOnlyStream forward = new(target))
        using (ZipArchive zip = new(forward, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry("report" + Path.GetExtension(inner), CompressionLevel.NoCompression);
            using Stream entryStream = entry.Open();
            entryStream.Write(Sample(inner));
        }

        byte[] bytes = target.ToArray();
        Assert.True((BitConverter.ToUInt16(bytes, 6) & 0x08) != 0, "precondition: the outer entry carries a data descriptor");
        Assert.Same(FileFormat.Unknown, Match(bytes, 1 << 20));
    }

    [Theory]
    [InlineData("sample.docx", CompressionLevel.Optimal)]
    [InlineData("sample.xlsx", CompressionLevel.Optimal)]
    [InlineData("sample.pptx", CompressionLevel.Optimal)]
    [InlineData("sample.docx", CompressionLevel.Fastest)]
    [InlineData("sample.xlsx", CompressionLevel.Fastest)]
    [InlineData("sample.pptx", CompressionLevel.Fastest)]
    [InlineData("sample.docx", CompressionLevel.SmallestSize)]
    public void Review_StreamedZipThatDeflatesAnOfficeFileInside_IsNotThatDocument(string inner, CompressionLevel level)
    {
        // An Office file is already compressed, so a deflate encoder stores its blocks raw: the inner local headers
        // can appear in the compressed data of the outer entry, which carries a data descriptor.
        using MemoryStream target = new();
        using (ForwardOnlyStream forward = new(target))
        using (ZipArchive zip = new(forward, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry("report" + Path.GetExtension(inner), level);
            using Stream entryStream = entry.Open();
            entryStream.Write(Sample(inner));
        }

        byte[] bytes = target.ToArray();
        Assert.True((BitConverter.ToUInt16(bytes, 6) & 0x08) != 0, "precondition: the outer entry carries a data descriptor");
        Assert.Equal(8, BitConverter.ToUInt16(bytes, 8));
        Assert.Same(FileFormat.Unknown, Match(bytes, 1 << 20));
    }

    [Theory]
    [InlineData("sample.docx")]
    [InlineData("sample.xlsx")]
    [InlineData("sample.pptx")]
    public void Review_StreamedZipWhoseDeflateDataHoldsStoredBlocksOfAnOfficeFile_IsNotThatDocument(string inner)
    {
        // Deflate allows raw "stored" blocks, which encoders use for incompressible data such as an Office file:
        // the inner local headers then lie verbatim in the compressed data of the outer entry (method 8, data descriptor).
        byte[] payload = Sample(inner);
        using MemoryStream ms = new();
        BinaryWriter w = new(ms);
        byte[] name = Encoding.ASCII.GetBytes("report.bin");
        w.Write(0x04034B50u); w.Write((ushort)20); w.Write((ushort)0x08); w.Write((ushort)8); w.Write(0u); w.Write(0u); w.Write(0u); w.Write(0u);
        w.Write((ushort)name.Length); w.Write((ushort)0); w.Write(name);
        long dataStart = ms.Position;
        for (int at = 0; at < payload.Length; at += 65535)
        {
            ushort len = (ushort)Math.Min(65535, payload.Length - at);
            w.Write((byte)(at + len >= payload.Length ? 1 : 0)); w.Write(len); w.Write((ushort)~len); w.Write(payload, at, len);
        }
        w.Write(0x08074B50u); w.Write(0u); w.Write((uint)(ms.Position - dataStart)); w.Write((uint)payload.Length);
        w.Flush();

        Assert.Same(FileFormat.Unknown, Match(ms.ToArray(), 1 << 20));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Review_StreamedPackageWithStoredBlocks_IsConfirmed_With32And64BitDescriptors(bool wide)
    {
        // A streamed Office package whose entries use raw deflate blocks (method 8, data descriptor): both descriptor widths must be followed.
        using MemoryStream ms = new();
        BinaryWriter w = new(ms);
        foreach ((string entryName, string content) in new[] { ("[Content_Types].xml", "<Types/>"), ("word/document.xml", "<w:document/>") })
        {
            byte[] name = Encoding.ASCII.GetBytes(entryName);
            byte[] payload = Encoding.ASCII.GetBytes(content);
            w.Write(0x04034B50u); w.Write((ushort)20); w.Write((ushort)0x08); w.Write((ushort)8); w.Write(0u); w.Write(0u); w.Write(0u); w.Write(0u);
            w.Write((ushort)name.Length); w.Write((ushort)0); w.Write(name);
            long dataStart = ms.Position;
            w.Write((byte)1); w.Write((ushort)payload.Length); w.Write((ushort)~payload.Length); w.Write(payload);
            w.Write(0x08074B50u); w.Write(0u);
            if (wide)
            {
                w.Write((ulong)(ms.Position - 8 - dataStart)); w.Write((ulong)payload.Length);
            }
            else
            {
                w.Write((uint)(ms.Position - 8 - dataStart)); w.Write((uint)payload.Length);
            }
        }

        w.Flush();
        Assert.Same(FileFormat.Docx, Match(ms.ToArray(), 1 << 20));
    }

    private sealed class ForwardOnlyStream(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = zip.CreateEntry(name, name == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
        return data.AsSpan().IndexOf(pattern);
    }
}
