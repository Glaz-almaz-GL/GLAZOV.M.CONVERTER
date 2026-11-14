using GLAZOV.M.CONVERTER.Data;
using System.Text;
using DetectorChain = GLAZOV.M.CONVERTER.Detection.DetectorChain;

namespace GLAZOV.M.CONVERTER.Formats.Tests.Detection;

/// <summary>
/// Plain text by its content: UTF-8 with and without a byte order mark, UTF-16 with a mark, no control characters;
/// binary data, random bytes and an empty input are not text; whatever else a file starts like stays that type.
/// </summary>
public sealed class TextSignatureTests
{
    private static FileFormat Detect(byte[] bytes)
    {
        return new DetectorChain(TestDetectors.Create()).Detect(bytes);
    }

    public static TheoryData<string> Texts =>
    [
        "Hello, world.",
        "Привет, мир! Русский текст — с тире, «кавычками» и ёжиком.",
        "строка 1\nстрока 2\r\nстрока 3\tс табуляцией\fи переводом страницы",
        "日本語のテキスト and emoji 😀 outside the BMP",
        "# Заголовок\n\n- пункт\n",
        "x",
    ];

    [Theory]
    [MemberData(nameof(Texts))]
    public void Utf8_WithAndWithoutAMark_IsText(string text)
    {
        Assert.Equal(FileFormat.Text, Detect(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(FileFormat.Text, Detect([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void Utf16_WithAMark_IsText_BothByteOrders(string text)
    {
        Assert.Equal(FileFormat.Text, Detect([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)]));
        Assert.Equal(FileFormat.Text, Detect([.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(text)]));
    }

    [Fact]
    public void Utf16_WithoutAMark_IsNotText()
    {
        Assert.Equal(FileFormat.Unknown, Detect(Encoding.Unicode.GetBytes("Привет, мир")));
    }

    [Fact]
    public void Empty_AndMarkOnly_AreNotText()
    {
        Assert.Equal(FileFormat.Unknown, Detect([]));
        Assert.Equal(FileFormat.Unknown, Detect([0xEF, 0xBB, 0xBF]));
        Assert.Equal(FileFormat.Unknown, Detect([0xD0]));
        Assert.Equal(FileFormat.Unknown, Detect([0xFF, 0xFE]));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0x07)]
    [InlineData(0x08)]
    [InlineData(0x0B)]
    [InlineData(0x1B)]
    [InlineData(0x1F)]
    [InlineData(0x7F)]
    public void AControlByte_AnywhereInTheWindow_MakesItNotText(byte control)
    {
        byte[] text = Encoding.UTF8.GetBytes("Привет, мир. Hello, world.");
        byte[] atStart = [control, .. text];
        byte[] inMiddle = [.. text.Take(10), control, .. text.Skip(10)];
        byte[] atEnd = [.. text, control];

        Assert.Equal(FileFormat.Unknown, Detect(atStart));
        Assert.Equal(FileFormat.Unknown, Detect(inMiddle));
        Assert.Equal(FileFormat.Unknown, Detect(atEnd));
    }

    [Theory]
    [InlineData(new byte[] { 0xC0, 0xAF })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF })]
    [InlineData(new byte[] { 0x80 })]
    [InlineData(new byte[] { 0xF5, 0x80, 0x80, 0x80 })]
    public void InvalidUtf8_IsNotText(byte[] bad)
    {
        Assert.Equal(FileFormat.Unknown, Detect([.. "abc "u8.ToArray(), .. bad, .. " abc"u8.ToArray()]));
    }

    [Theory]
    [InlineData(0xFF)]
    [InlineData(0xFE)]
    [InlineData(0xF8)]
    [InlineData(0xF5)]
    [InlineData(0xC0)]
    [InlineData(0xC1)]
    public void AByteThatCanNeverStartUtf8_AtTheEnd_IsNotText(byte bad)
    {
        Assert.Equal(FileFormat.Unknown, Detect([.. "abc def"u8.ToArray(), bad]));
    }

    [Fact]
    public void ASequenceCutByTheEndOfTheWindow_IsNotHeldAgainstTheText()
    {
        byte[] whole = Encoding.UTF8.GetBytes("Привет, мир 😀");
        for (int cut = 2; cut < whole.Length; cut++)
        {
            Assert.Equal(FileFormat.Text, Detect(whole[..cut]));
        }
    }

    [Fact]
    public void Utf16_BrokenSurrogates_AreNotText_ButACutPairIsFine()
    {
        byte[] lowFirst = [.. Encoding.Unicode.GetPreamble(), 0x00, 0xDC, 0x41, 0x00];
        byte[] highAlone = [.. Encoding.Unicode.GetPreamble(), 0x00, 0xD8, 0x41, 0x00];
        byte[] cut = [.. Encoding.Unicode.GetPreamble(), 0x41, 0x00, 0x3D, 0xD8];

        Assert.Equal(FileFormat.Unknown, Detect(lowFirst));
        Assert.Equal(FileFormat.Unknown, Detect(highAlone));
        Assert.Equal(FileFormat.Text, Detect(cut));
    }

    [Fact]
    public void RandomBytes_AreNotText()
    {
        Random random = new(20261005);
        for (int i = 0; i < 2000; i++)
        {
            byte[] bytes = new byte[64];
            random.NextBytes(bytes);

            Assert.NotEqual(FileFormat.Text, Detect(bytes));
        }
    }

    [Theory]
    [InlineData("sample.png", "PNG")]
    [InlineData("sample.jpg", "JPEG")]
    [InlineData("sample.html", "HTML")]
    [InlineData("sample.rtf", "RTF")]
    [InlineData("sample-plain.svg", "SVG")]
    [InlineData("sample.docx", "DOCX")]
    public void WhatElseAFileStartsLike_StaysThatType(string file, string name)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", file));

        Assert.Equal(name, Detect(bytes).Name);
    }

    [Fact]
    public void AnExtensionDetector_IsAskedBeforeText()
    {
        byte[] custom = "DEMO: plain ascii that an extension owns"u8.ToArray();
        DetectorChain chain = new([.. TestDetectors.Create(), new Prefix("DEMO:", FileFormat.Create("Demo", "application/x-demo", "demo"))]);

        Assert.Equal("Demo", chain.Detect(custom).Name);
        Assert.Equal(FileFormat.Text, chain.Detect("not claimed by anyone"u8.ToArray()));
    }

    [Fact]
    public void TheChainReadsAWindowEvenWithoutDetectors()
    {
        Assert.True(new DetectorChain([]).HeadLength >= 4096);
    }

    private sealed class Prefix(string start, FileFormat format) : Extensions.IFormatDetector
    {
        public int HeadLength => 16;

        FileFormat? Extensions.IFormatDetector.Detect(ReadOnlySpan<byte> head)
        {
            return head.StartsWith(Encoding.ASCII.GetBytes(start)) ? format : null;
        }
    }
}
