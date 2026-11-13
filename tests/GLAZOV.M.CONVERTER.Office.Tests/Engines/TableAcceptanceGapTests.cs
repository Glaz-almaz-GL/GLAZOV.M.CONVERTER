using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// Gaps between the table conversions and what the records promise (defects 023-026); each test fails until its defect is fixed.
/// </summary>
[Collection("LibreOffice")]
public sealed class TableAcceptanceGapTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convgap-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private Converter Make(OfficeSettings? office = null)
    {
        string work = Path.Combine(_temp, "work");
        Directory.CreateDirectory(work);
        return OfficeConverter.Create(new ConverterSettings { TempFolder = work, DetectionWindow = 4 * 1024 * 1024 }, office);
    }

    private string Put(string name, byte[] bytes)
    {
        string path = Path.Combine(_temp, "in", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string Utf8(byte[] bytes)
    {
        return new UTF8Encoding(false).GetString(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()) ? bytes[3..] : bytes);
    }

    [OfficeFact]
    public async Task Windows1251Csv_AsInput_IsReadWithTheInputEncoding()
    {
        string path = Put("w.csv", Encoding.GetEncoding(1251).GetBytes("Имя,Город\r\nИван,Москва\r\nПётр,Казань\r\n"));

        ConversionResult result = await Make().ConvertAsync(Source.File(path), Target.Memory(FileFormat.Csv));

        Assert.Contains("Москва", Utf8(result.Bytes!));
    }

    [OfficeFact]
    public async Task Csv_ToCsv_KeepsLeadingZerosAndExponentLikeText()
    {
        string path = Put("z.csv", Encoding.UTF8.GetBytes("code,qty\n007,1e3\n001,2\n"));

        ConversionResult result = await Make().ConvertAsync(Source.File(path), Target.Memory(FileFormat.Csv));

        Assert.Contains("007,1e3", Utf8(result.Bytes!));
    }

    [OfficeFact]
    public async Task Book_TallerThanXls_IsRefusedNotCut()
    {
        string csv = Put("tall.csv", Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("a,b\n", 70_000))));
        string xlsx = Path.Combine(_temp, "t.xlsx");
        await Make().ConvertAsync(Source.File(csv), Target.File(xlsx, FileFormat.Xlsx));

        ConversionException error = await Assert.ThrowsAsync<ConversionException>(
            () => Make().ConvertAsync(Source.File(xlsx), Target.File(Path.Combine(_temp, "t.xls"), FileFormat.Xls)));

        Assert.Equal(ConversionFailureReason.InvalidParameters, error.Reason);
    }

    [OfficeFact]
    public async Task SheetNumber_DoesNotBreakAPlainCsv()
    {
        string path = Put("a.csv", Encoding.UTF8.GetBytes("a,b\n1,2\n3,4\n"));

        ConversionResult result = await Make(new OfficeSettings { SheetNumber = 2 }).ConvertAsync(Source.File(path), Target.Memory(FileFormat.Tsv));

        Assert.Contains("1\t2", Utf8(result.Bytes!));
    }
}
