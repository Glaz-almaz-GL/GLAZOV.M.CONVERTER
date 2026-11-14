using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Office.Engines;
using GLAZOV.M.CONVERTER.Settings;
using System.Text;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// Tables into each other through the real LibreOffice (skipped without it): a book of three sheets to a folder and to one file,
/// text in several encodings there and back, formulas that stay text, the limits of the old XLS, text as a one-column table, a table as PDF.
/// </summary>
[Collection("LibreOffice")]
public sealed class TableEngineTests : IDisposable
{
    private static readonly Encoding Cp1251 = Encodings.Find("windows-1251", "test");

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtable-" + Guid.NewGuid().ToString("N"));

    public TableEngineTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    public static TheoryData<string> Books => ["xlsx", "xls", "ods"];

    private Converter Make(OfficeSettings? office = null, bool overwrite = false)
    {
        string folder = Path.Combine(_temp, "work");
        Directory.CreateDirectory(folder);
        ConverterSettings settings = new() { TempFolder = folder, DetectionWindow = 4 * 1024 * 1024, OverwriteExisting = overwrite };
        return OfficeConverter.Create(settings, office);
    }

    private static string Book(string extension)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", "sample-book." + extension);
    }

    private string Put(string name, byte[] bytes)
    {
        string path = Path.Combine(_temp, "in", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string Out => Path.Combine(_temp, "out");

    private string[] LeftInWork()
    {
        string folder = Path.Combine(_temp, "work");
        return Directory.Exists(folder) ? Directory.GetFileSystemEntries(folder) : [];
    }

    private static string Utf8(byte[] bytes)
    {
        return new UTF8Encoding(false).GetString(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()) ? bytes[3..] : bytes);
    }

    [OfficeTheory]
    [MemberData(nameof(Books))]
    public async Task Book_ToAFolder_GivesAFilePerSheet_NamedAfterTheInputAndTheSheet(string extension)
    {
        ConversionResult result = await Make().ConvertAsync(Source.File(Book(extension)), Target.Folder(Out, FileFormat.Csv));

        string[] names = [.. result.OutputPaths.Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal)];
        Assert.Equal(["sample-book-Alpha.csv", "sample-book-Zeta.csv", "sample-book-Итог.csv"], names);
        Assert.Equal(names, Directory.GetFiles(Out).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal("a1", Utf8(await File.ReadAllBytesAsync(Path.Combine(Out, "sample-book-Alpha.csv"))).Trim());
        Assert.Contains("\"a,b \"\"q\"\"\"", Utf8(await File.ReadAllBytesAsync(Path.Combine(Out, "sample-book-Итог.csv"))));
        Assert.Contains(Path.Combine(Path.GetFullPath(Out), "sample-book-Итог.csv"), result.OutputPaths);
        Assert.Empty(LeftInWork());
    }

    [OfficeFact]
    public async Task Book_ToTsvFolder_UsesTabsAndTheTsvExtension()
    {
        ConversionResult result = await Make().ConvertAsync(Source.File(Book("xlsx")), Target.Folder(Out, FileFormat.Tsv));

        Assert.All(result.OutputPaths, path => Assert.EndsWith(".tsv", path, StringComparison.Ordinal));
        Assert.Contains("Имя\t", Utf8(await File.ReadAllBytesAsync(Path.Combine(Out, "sample-book-Итог.tsv"))));
    }

    [OfficeFact]
    public async Task Book_ToOneFile_TakesTheFirstSheet_StartsWithTheMark_AndSaysSo()
    {
        string output = Path.Combine(Out, "one.csv");

        ConversionResult result = await Make().ConvertAsync(Source.File(Book("xlsx")), Target.File(output, FileFormat.Csv));

        byte[] bytes = await File.ReadAllBytesAsync(output);
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.StartsWith("Имя,", Utf8(bytes));
        Assert.Equal([output], result.OutputPaths);
        Assert.Equal(["в книге 3 листов, взят лист «Итог»"], result.Notes);
    }

    [OfficeFact]
    public async Task Book_ToMemory_SheetNumberFromTheSettings_NoMarkWhenOff()
    {
        OfficeSettings office = new() { SheetNumber = 2, CsvOutputByteOrderMark = false };

        ConversionResult result = await Make(office).ConvertAsync(Source.File(Book("ods")), Target.Memory(FileFormat.Csv));

        Assert.Equal("z1", Utf8(result.Bytes!).Trim());
        Assert.False(result.Bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Contains("«Zeta»", result.Notes.Single());
    }

    [OfficeFact]
    public async Task Book_SheetNumberBeyondTheBook_IsInvalidParameters()
    {
        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => Make(new OfficeSettings { SheetNumber = 4 }).ConvertAsync(Source.File(Book("xlsx")), Target.Memory(FileFormat.Csv)));

        Assert.Equal(ConversionFailureReason.InvalidParameters, error.Reason);
        Assert.Contains("3 листов", error.Message);
        Assert.Empty(LeftInWork());
    }

    [OfficeFact]
    public async Task Book_ExistingFileInTheFolder_IsNotOverwritten_AndNoSheetIsWritten()
    {
        Directory.CreateDirectory(Out);
        await File.WriteAllTextAsync(Path.Combine(Out, "sample-book-Zeta.csv"), "mine");

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => Make().ConvertAsync(Source.File(Book("xlsx")), Target.Folder(Out, FileFormat.Csv)));

        Assert.Equal(ConversionFailureReason.FileExists, error.Reason);
        Assert.Equal(["sample-book-Zeta.csv"], Directory.GetFileSystemEntries(Out).Select(Path.GetFileName));
    }

    [OfficeFact]
    public async Task Book_ToBook_KeepsTheSheets()
    {
        string output = Path.Combine(Out, "copy.ods");

        await Make().ConvertAsync(Source.File(Book("xlsx")), Target.File(output, FileFormat.Ods));
        ConversionResult back = await Make().ConvertAsync(Source.File(output), Target.Memory(FileFormat.Csv));

        Assert.Equal(3, int.Parse(back.Notes.Single().Split(' ')[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    [OfficeFact]
    public async Task Csv_ToWindows1251_AndSemicolons_WritesThoseBytes()
    {
        string text = "Имя;Город\r\nИван;Москва\r\nПётр;Казань\r\n";
        string source = Put("люди.csv", Encoding.UTF8.GetBytes(text));
        string output = Path.Combine(Out, "b.csv");

        await Make(new OfficeSettings { CsvDelimiter = ';', CsvOutputEncoding = "windows-1251" }).ConvertAsync(Source.File(source), Target.File(output, FileFormat.Csv));

        Assert.Equal(Cp1251.GetBytes(text), await File.ReadAllBytesAsync(output));
    }

    [OfficeFact]
    public async Task Csv_Utf8WithAndWithoutMark_ReadTheSame()
    {
        string text = "Имя,Город\nИван,Москва\nПётр,Казань\n";
        string plain = Put("a.csv", Encoding.UTF8.GetBytes(text));
        string marked = Put("b.csv", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]);

        ConversionResult first = await Make().ConvertAsync(Source.File(plain), Target.Memory(FileFormat.Csv));
        ConversionResult second = await Make().ConvertAsync(Source.File(marked), Target.Memory(FileFormat.Csv));

        Assert.Equal(Utf8(first.Bytes!), Utf8(second.Bytes!));
        Assert.Contains("Москва", Utf8(first.Bytes!));
    }

    [OfficeFact]
    public async Task Csv_WithSemicolons_AndTheDefaultSeparator_IsReadBySemicolons_WrittenWithCommas()
    {
        string source = Put("s.csv", Encoding.UTF8.GetBytes("a;b;c\n1;2;3\n4;5;6\n"));

        ConversionResult result = await Make().ConvertAsync(Source.File(source), Target.Memory(FileFormat.Csv));

        Assert.Equal("a,b,c\r\n1,2,3\r\n4,5,6", Utf8(result.Bytes!).Trim());
    }

    [OfficeFact]
    public async Task CsvToTsv_AndBack_ChangesOnlyTheSeparator()
    {
        string source = Put("c.csv", Encoding.UTF8.GetBytes("a,b\n\"x,y\",2\n3,4\n"));
        string tsv = Path.Combine(Out, "c.tsv");
        string csv = Path.Combine(Out, "c2.csv");

        await Make().ConvertAsync(Source.File(source), Target.File(tsv, FileFormat.Tsv));
        await Make().ConvertAsync(Source.File(tsv), Target.File(csv, FileFormat.Csv));

        Assert.Equal("a\tb\r\nx,y\t2\r\n3\t4", Utf8(await File.ReadAllBytesAsync(tsv)).Trim());
        Assert.Equal("a,b\r\n\"x,y\",2\r\n3,4", Utf8(await File.ReadAllBytesAsync(csv)).Trim());
    }

    [OfficeFact]
    public async Task FormulaInAnInputCsv_StaysText()
    {
        string source = Put("f.csv", Encoding.UTF8.GetBytes("a,b\n=1+1,@SUM(1)\n3,4\n"));

        ConversionResult result = await Make().ConvertAsync(Source.File(source), Target.Memory(FileFormat.Csv));

        Assert.Contains("=1+1", Utf8(result.Bytes!));
        Assert.DoesNotContain("\r\n2,", Utf8(result.Bytes!));
    }

    [OfficeFact]
    public async Task CharacterTheOutputEncodingCannotHold_IsInvalidParameters()
    {
        string source = Put("j.csv", Encoding.UTF8.GetBytes("a,b\nこんにちは,2\n3,4\n"));

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => Make(new OfficeSettings { CsvOutputEncoding = "windows-1251" }).ConvertAsync(Source.File(source), Target.Memory(FileFormat.Csv)));

        Assert.Equal(ConversionFailureReason.InvalidParameters, error.Reason);
        Assert.Contains("windows-1251", error.Message);
    }

    [OfficeFact]
    public async Task CsvTooTallForXls_IsRefusedWithTheLimit_NotCut()
    {
        string source = Put("tall.csv", Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("a,b\n", 65_537))));

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => Make().ConvertAsync(Source.File(source), Target.File(Path.Combine(Out, "t.xls"), FileFormat.Xls)));

        Assert.Equal(ConversionFailureReason.InvalidParameters, error.Reason);
        Assert.Contains("65536", error.Message);
        Assert.False(File.Exists(Path.Combine(Out, "t.xls")));
        Assert.Empty(LeftInWork());
    }

    [OfficeFact]
    public async Task Text_ToXlsx_IsOneColumn_AndBackToCsv()
    {
        string source = Put("заметка.txt", Encoding.UTF8.GetBytes("первая строка\nвторая строка\nтретья строка\n"));
        string xlsx = Path.Combine(Out, "t.xlsx");

        await Make().ConvertAsync(Source.File(source), Target.File(xlsx, FileFormat.Xlsx));
        ConversionResult back = await Make().ConvertAsync(Source.File(xlsx), Target.Memory(FileFormat.Csv));

        Assert.Equal("первая строка\r\nвторая строка\r\nтретья строка", Utf8(back.Bytes!).Trim());
    }

    [OfficeFact]
    public async Task Csv_ToPdf_IsAPdf()
    {
        string source = Put("p.csv", Encoding.UTF8.GetBytes("Имя,Город\nИван,Москва\nПётр,Казань\n"));
        string pdf = Path.Combine(Out, "p.pdf");

        await Make().ConvertAsync(Source.File(source), Target.File(pdf, FileFormat.Pdf));

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(await File.ReadAllBytesAsync(pdf), 0, 4), StringComparison.Ordinal);
        Assert.Empty(LeftInWork());
    }

    [OfficeFact]
    public async Task Stream_Input_IsAccepted_AndLeavesNothingBehind()
    {
        await using MemoryStream input = new(Encoding.UTF8.GetBytes("a,b\n1,2\n3,4\n"));

        ConversionResult result = await Make().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Tsv));

        Assert.Equal("a\tb\r\n1\t2\r\n3\t4", Utf8(result.Bytes!).Trim());
        Assert.Empty(LeftInWork());
    }

    [OfficeFact]
    public async Task Batch_BooksToAFolder_ReportsEveryFile()
    {
        string folder = Path.Combine(_temp, "books");
        Directory.CreateDirectory(folder);
        File.Copy(Book("xlsx"), Path.Combine(folder, "one.xlsx"));
        File.Copy(Book("ods"), Path.Combine(folder, "two.ods"));

        GLAZOV.M.CONVERTER.Batch.BatchReport report = await Make().ConvertBatchAsync(Source.Folder(folder), Target.Folder(Out, FileFormat.Csv));

        Assert.Equal(2, report.Succeeded);
        Assert.Equal(6, report.Items.Sum(item => item.OutputPaths.Count));
        Assert.Equal(6, Directory.GetFiles(Out).Length);
    }
}
