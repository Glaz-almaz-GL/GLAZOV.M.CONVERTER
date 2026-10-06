using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;
using UglyToad.PdfPig;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// Text to PDF through LibreOffice: a file named .md or .markdown is laid out as Markdown, anything else is plain text;
/// the name only hints the layout and proves nothing. Needs the real LibreOffice, otherwise skipped.
/// </summary>
[Collection("LibreOffice")]
public sealed class LibreOfficeTextTests : IDisposable
{
    private const string Markdown = "# Заголовок первый\n\n- первый пункт\n- второй пункт\n\n**жирный** текст\n\n| а | б |\n|---|---|\n| 1 | 2 |\n";

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convoffice-text-" + Guid.NewGuid().ToString("N"));

    public LibreOfficeTextTests()
    {
        Directory.CreateDirectory(Path.Combine(_temp, "work"));
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    private Converter Converter()
    {
        return OfficeConverter.Create(new ConverterSettings { TempFolder = Path.Combine(_temp, "work") });
    }

    private string Put(string name, byte[] bytes)
    {
        string path = Path.Combine(_temp, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private async Task<string> TextOfAsync(string path)
    {
        ConversionResult result = await Converter().ConvertAsync(Source.File(path), Target.Memory(FileFormat.Pdf));

        Assert.Equal(FileFormat.Text, result.SourceType);
        using PdfDocument pdf = PdfDocument.Open(result.Bytes!);
        return string.Join('\n', pdf.GetPages().Select(page => page.Text));
    }

    [OfficeTheory]
    [InlineData("записка.md")]
    [InlineData("ЗАПИСКА.MD")]
    [InlineData("записка.markdown")]
    public async Task MarkdownName_GivesALaidOutPage(string name)
    {
        string text = await TextOfAsync(Put(name, Encoding.UTF8.GetBytes(Markdown)));

        Assert.Contains("Заголовок первый", text);
        Assert.Contains("первый пункт", text);
        Assert.Contains("жирный", text);
        Assert.DoesNotContain("#", text);
        Assert.DoesNotContain("**", text);
        Assert.DoesNotContain("|---", text);
    }

    [OfficeTheory]
    [InlineData("записка.txt")]
    [InlineData("записка.docx")]
    [InlineData("записка")]
    public async Task OtherNames_GiveThePlainText_AsItIs(string name)
    {
        string text = await TextOfAsync(Put(name, Encoding.UTF8.GetBytes(Markdown)));

        Assert.Contains("# Заголовок первый", text);
        Assert.Contains("**жирный**", text);
    }

    [OfficeFact]
    public async Task Stream_HasNoName_SoItIsPlainText()
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(Markdown));

        ConversionResult result = await Converter().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf));

        Assert.Equal(FileFormat.Text, result.SourceType);
        using PdfDocument pdf = PdfDocument.Open(result.Bytes!);
        Assert.Contains("# Заголовок первый", pdf.GetPage(1).Text);
    }

    [OfficeTheory]
    [InlineData("utf8")]
    [InlineData("utf8-mark")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    public async Task Cyrillic_IsReadRight_InEveryEncodingTheDetectorAccepts(string kind)
    {
        const string line = "Привет, мир: съешь ещё этих мягких французских булок.";
        byte[] bytes = kind switch
        {
            "utf8" => Encoding.UTF8.GetBytes(line),
            "utf8-mark" => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(line)],
            "utf16le" => [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(line)],
            _ => [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(line)],
        };

        string text = await TextOfAsync(Put(kind + ".txt", bytes));

        Assert.Contains("съешь ещё этих мягких", text);
    }

    [OfficeFact]
    public async Task ATextFile_UnderADocxName_IsText_TheNameProvesNothing()
    {
        string path = Put("не-документ.docx", Encoding.UTF8.GetBytes("Просто слова, а не документ Word."));

        string text = await TextOfAsync(path);

        Assert.Contains("Просто слова", text);
    }

    [OfficeFact]
    public async Task ABinaryFile_UnderATxtName_IsNotText_AndIsRefused()
    {
        string path = Put("двоичный.txt", [0x00, 0x01, 0x02, 0x9F, 0xFF, 0x10, 0x00]);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Converter().ConvertAsync(Source.File(path), Target.Memory(FileFormat.Pdf)));

        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
    }

    [OfficeFact]
    public async Task AnEmptyFile_IsRefused()
    {
        string path = Put("пусто.txt", []);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Converter().ConvertAsync(Source.File(path), Target.Memory(FileFormat.Pdf)));

        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
    }
}
