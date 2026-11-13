using System.Text;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats.Detection.Tables;
using DetectorChain = GLAZOV.M.CONVERTER.Detection.DetectorChain;

namespace GLAZOV.M.CONVERTER.Formats.Tests.Detection;

/// <summary>
/// CSV and TSV by content: at least three rows with the same non-zero number of delimiters outside quotes; quotes that hold a delimiter or a line break;
/// a byte order mark; and text that only looks like a table (one column, ragged rows, JSON, markup, prose) stays text.
/// </summary>
public sealed class TableSignatureTests
{
    private static FileFormat Detect(byte[] bytes)
    {
        return new DetectorChain(TestDetectors.Create()).Detect(bytes);
    }

    private static FileFormat Detect(string text)
    {
        return Detect(Encoding.UTF8.GetBytes(text));
    }

    [Theory]
    [InlineData("sample-table.csv", "CSV")]
    [InlineData("sample-table-semicolon.csv", "CSV")]
    [InlineData("sample-table.tsv", "TSV")]
    public void RealFiles_AreNamedByTheirDelimiter(string file, string name)
    {
        Assert.Equal(name, Detect(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", file))).Name);
    }

    [Fact]
    public void QuotedDelimitersAndLineBreaks_DoNotCount()
    {
        Assert.Equal(FileFormat.Csv, Detect("a,b\n\"x,y,z\",\"p\nq\"\n1,2\n"));
        Assert.Equal(FileFormat.Csv, Detect("a,b\n\"he said \"\"hi, you\"\"\",2\n3,4\n"));
        Assert.Equal(FileFormat.Tsv, Detect("a\tb\n\"x\ty\tz\"\t2\n3\t4\n"));
    }

    [Fact]
    public void ByteOrderMark_AndUtf16_AreRead()
    {
        Assert.Equal(FileFormat.Csv, Detect([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("a,b\n1,2\n3,4\n")]));
        Assert.Equal(FileFormat.Csv, Detect([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("a,b\r\n1,2\r\n3,4\r\n")]));
        Assert.Equal(FileFormat.Tsv, Detect([.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes("a\tb\n1\t2\n3\t4\n")]));
    }

    [Theory]
    [InlineData("a,b\n1,2\n3,4")]
    [InlineData("a,b\r1,2\r3,4\r")]
    [InlineData("\n\na,b\n\n1,2\n\n3,4\n\n")]
    [InlineData("a;b;c\n1;2;3\n4;5;6\n")]
    public void LineBreaksOfAnyKind_AndABlankRow_AreFine(string text)
    {
        Assert.Equal(FileFormat.Csv, Detect(text));
    }

    [Theory]
    [InlineData("name\nAnna\nBoris\n")]
    [InlineData("a,b\n1,2,3\n")]
    [InlineData("Hello, world\nGoodbye, world\n")]
    [InlineData("x=1;\ny=2;\n")]
    [InlineData("a,b\n1,2\n")]
    [InlineData("a,b\nonly one row without a second\n")]
    [InlineData("a,b")]
    [InlineData("a,b\n")]
    [InlineData("a,b\n\"1,2\n")]
    [InlineData("Hello, world.\nThis is plain prose, with commas, here and there.\nIt does not repeat itself.\n")]
    [InlineData("# Title\n\n- one, two\n- three\n")]
    [InlineData("| a | b |\n|---|---|\n| 1 | 2 |\n")]
    [InlineData("")]
    public void TextThatOnlyLooksLikeATable_StaysTextOrUnknown(string text)
    {
        Assert.NotEqual(FileFormat.Csv, Detect(text));
        Assert.NotEqual(FileFormat.Tsv, Detect(text));
    }

    [Theory]
    [InlineData("{\"a\":1,\"b\":2}\n{\"a\":3,\"b\":4}\n")]
    [InlineData("[1,2,3]\n[4,5,6]\n")]
    [InlineData("{\n  \"a\": 1,\n  \"b\": 2\n}\n")]
    [InlineData("<r><a>1</a>,<b>2</b></r>\n<r><a>3</a>,<b>4</b></r>\n")]
    public void JsonAndMarkup_AreNotTables(string text)
    {
        Assert.Equal(FileFormat.Text, Detect(text));
    }

    [Fact]
    public void BinaryData_AndWindows1251Text_AreNotTables()
    {
        Assert.Equal(FileFormat.Unknown, Detect([.. "a,b\n1,"u8.ToArray(), 0x00, .. ",2\n"u8.ToArray()]));
        Assert.Equal(FileFormat.Unknown, Detect(Encoding.Latin1.GetBytes("a,b\n\u00cf\u00f0\u00e8,2\n")));
    }

    [Fact]
    public void ACutWindow_IsJudgedByTheRowsThatEndedInIt()
    {
        string rows = string.Concat(Enumerable.Repeat("abcdefghij,klmnopqrstuv,wxyz\n", 400));

        for (int cut = 1; cut < rows.Length; cut += 7)
        {
            Assert.NotEqual(FileFormat.Tsv, Detect(rows[..cut]));
        }

        Assert.Equal(FileFormat.Csv, Detect(rows));
    }

    [Fact]
    public void EverySample_IsATableOnlyIfItsNameSaysSo()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Samples");
        foreach (string path in Directory.EnumerateFiles(folder))
        {
            byte[] bytes = File.ReadAllBytes(path);
            byte[] head = bytes.AsSpan(0, Math.Min(bytes.Length, 64 * 1024)).ToArray();
            FileFormat? named = new TableDetector().Detect(head);

            Assert.True(
                (named is not null) == Path.GetFileName(path).StartsWith("sample-table", StringComparison.Ordinal),
                $"{Path.GetFileName(path)}: table detector said {named?.Name ?? "nothing"}");
        }
    }

    [Fact]
    public void TheTableDetector_IsAskedBeforeText()
    {
        Assert.True(new FormatsExtension().Detectors.ToList().IndexOf(StandardDetectors.Tables) < new FormatsExtension().Detectors.ToList().IndexOf(StandardDetectors.Text));
    }
}
