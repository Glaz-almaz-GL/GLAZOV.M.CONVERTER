using System.Text.RegularExpressions;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Office.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;
using UglyToad.PdfPig;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// Documents to PDF through LibreOffice: the declared pairs (no program needed) and live runs on real files
/// (the program is needed; without it they are skipped). The PDF is read back with PdfPig.
/// </summary>
[Collection("LibreOffice")]
public sealed partial class LibreOfficeEngineTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convoffice-" + Guid.NewGuid().ToString("N"));

    public LibreOfficeEngineTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    public static TheoryData<string> Samples => ["docx", "doc", "odt", "rtf", "xlsx", "xls", "ods", "pptx", "ppt", "odp", "html"];

    [GeneratedRegex("[А-Яа-яЁё]")]
    private static partial Regex Cyrillic();

    private static string Sample(string extension)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", "sample." + extension);
    }

    private Converter WithTemp(Action<ConverterSettings>? configure = null)
    {
        string folder = Path.Combine(_temp, "work");
        Directory.CreateDirectory(folder);
        // The directory of an old PPT, DOC or XLS made by LibreOffice sits at the end of the file: a wide window is needed to confirm the type.
        ConverterSettings settings = new() { TempFolder = folder, DetectionWindow = 4 * 1024 * 1024 };
        configure?.Invoke(settings);
        return OfficeConverter.Create(settings);
    }

    private string[] LeftInWork()
    {
        string folder = Path.Combine(_temp, "work");
        return Directory.Exists(folder) ? Directory.GetFileSystemEntries(folder) : [];
    }

    [Fact]
    public void Engine_DeclaresTwelvePairs_NeedsTheProgram_NoOperations()
    {
        LibreOfficeEngine engine = new();

        Assert.Equal("soffice", Assert.Single(engine.Tools).Name);
        Assert.False(engine.SupportsOperations);
        Assert.Equal(12, engine.Pairs.Count);
        Assert.All(engine.Pairs, pair => Assert.Equal(FileFormat.Pdf, pair.To));
        Assert.Equal(
            [FileFormat.Docx, FileFormat.Doc, FileFormat.Odt, FileFormat.Rtf, FileFormat.Xlsx, FileFormat.Xls, FileFormat.Ods, FileFormat.Pptx, FileFormat.Ppt, FileFormat.Odp, FileFormat.Html, FileFormat.Text],
            engine.Pairs.Select(pair => pair.From));
    }

    [Fact]
    public void Catalog_ShowsTheTwelvePairs()
    {
        ConverterCapabilities caps = OfficeConverter.Create().Capabilities;

        Conversion[] documents = [.. caps.All.Where(c => c.To.Equals(FileFormat.Pdf))];

        Assert.Equal(12, documents.Length);
        Assert.True(caps.CanConvert(FileFormat.Docx, FileFormat.Pdf));
        Assert.False(caps.CanConvert(FileFormat.Pdf, FileFormat.Docx));
    }

    [Fact]
    public void WithoutThePackage_DocumentsAreNotOffered()
    {
        ConverterCapabilities caps = new Converter().Capabilities;

        Assert.False(caps.CanConvert(FileFormat.Docx, FileFormat.Pdf));
        Assert.DoesNotContain(caps.All, c => c.To.Equals(FileFormat.Pdf) && c.From.Equals(FileFormat.Docx));
    }

    [Fact]
    public async Task ProgramNotFound_IsToolNotFound_WithTheHint_AndNothingIsLeft()
    {
        Converter converter = WithTemp(s => s.ToolPaths["soffice"] = Path.Combine(_temp, "нет", "soffice"));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(Source.File(Sample("docx")), Target.Memory(FileFormat.Pdf)));

        Assert.Equal(ConversionFailureReason.ToolNotFound, ex.Reason);
        Assert.Contains("ToolPaths", ex.Message);
        Assert.Empty(LeftInWork());
    }

    [Fact]
    public async Task Operations_AreRefusedBeforeAnyWork()
    {
        Converter converter = WithTemp();

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(
                Source.File(Sample("docx")),
                Target.Memory(FileFormat.Pdf),
                new ConversionOptions { Operations = { Operation.RotateRight } }));

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
    }

    [OfficeTheory]
    [MemberData(nameof(Samples))]
    public async Task RealDocument_BecomesAReadablePdf(string extension)
    {
        ConversionResult result = await WithTemp().ConvertAsync(Source.File(Sample(extension)), Target.Memory(FileFormat.Pdf));

        Assert.True(result.Succeeded);
        Assert.Equal(FileFormat.Pdf, result.TargetFormat);
        using PdfDocument pdf = PdfDocument.Open(result.Bytes!);
        Assert.True(pdf.NumberOfPages >= 1);
        string text = string.Join(' ', pdf.GetPages().Select(page => page.Text));
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Empty(LeftInWork());
    }

    [OfficeTheory]
    [InlineData("docx")]
    [InlineData("odt")]
    [InlineData("pptx")]
    public async Task Cyrillic_SurvivesTheConversion(string extension)
    {
        ConversionResult result = await WithTemp().ConvertAsync(Source.File(Sample(extension)), Target.Memory(FileFormat.Pdf));

        using PdfDocument pdf = PdfDocument.Open(result.Bytes!);
        Assert.Matches(Cyrillic(), string.Join(' ', pdf.GetPages().Select(page => page.Text)));
    }

    [OfficeFact]
    public async Task StreamInput_Works_AndTheCopyIsRemoved()
    {
        await using FileStream input = File.OpenRead(Sample("docx"));

        ConversionResult result = await WithTemp().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf));

        Assert.True(result.Succeeded);
        Assert.Empty(LeftInWork());
    }

    [OfficeFact]
    public async Task FileWithAnotherExtension_IsConvertedByItsContent()
    {
        string odd = Path.Combine(_temp, "отчёт.dat");
        File.Copy(Sample("docx"), odd);

        ConversionResult result = await WithTemp().ConvertAsync(Source.File(odd), Target.Memory(FileFormat.Pdf));

        Assert.Equal(FileFormat.Docx, result.SourceType);
        using PdfDocument pdf = PdfDocument.Open(result.Bytes!);
        Assert.True(pdf.NumberOfPages >= 1);
    }

    [OfficeFact]
    public async Task FileToFile_WritesThePdf()
    {
        string target = Path.Combine(_temp, "результат.pdf");

        ConversionResult result = await WithTemp().ConvertAsync(Source.File(Sample("xlsx")), Target.File(target, FileFormat.Pdf));

        Assert.True(result.Succeeded);
        using PdfDocument pdf = PdfDocument.Open(await File.ReadAllBytesAsync(target));
        Assert.True(pdf.NumberOfPages >= 1);
    }

    [OfficeFact]
    public async Task EightRunsAtOnce_GiveEightResults()
    {
        string input = Path.Combine(_temp, "in");
        string output = Path.Combine(_temp, "pdfs");
        Directory.CreateDirectory(input);
        string[] kinds = ["docx", "odt", "xlsx", "ods", "pptx", "odp", "rtf", "html"];
        foreach (string kind in kinds)
        {
            File.Copy(Sample(kind), Path.Combine(input, "файл-" + kind + "." + kind));
        }

        BatchReport report = await WithTemp(s => s.MaxParallelTasks = 8).ConvertBatchAsync(Source.Folder(input), Target.Folder(output, FileFormat.Pdf));

        Assert.Equal(8, report.Succeeded);
        Assert.Equal(8, Directory.GetFiles(output, "*.pdf").Length);
        Assert.Empty(LeftInWork());
    }

    [OfficeFact]
    public async Task Cancellation_StopsTheProgram_AndLeavesNothingBehind()
    {
        using CancellationTokenSource cancel = new();
        Converter converter = WithTemp();
        Task<ConversionResult> run = converter.ConvertAsync(Source.File(Sample("pptx")), Target.Memory(FileFormat.Pdf), cancellationToken: cancel.Token);

        await Task.Delay(300);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run);
        Assert.Empty(LeftInWork());
    }

    [OfficeFact]
    public async Task DamagedDocument_IsToolFailed_WithAClearMessage()
    {
        string damaged = Path.Combine(_temp, "битый.docx");
        byte[] all = await File.ReadAllBytesAsync(Sample("docx"));
        // The start of the archive stays (so the type is confirmed), the middle is ruined.
        for (int i = all.Length / 3; i < all.Length; i++)
        {
            all[i] = 0x41;
        }

        await File.WriteAllBytesAsync(damaged, all);

        ConversionException ex = await Assert.ThrowsAnyAsync<ConversionException>(async () =>
            await WithTemp().ConvertAsync(Source.File(damaged), Target.Memory(FileFormat.Pdf)));

        Assert.True(ex.Reason is ConversionFailureReason.ToolFailed or ConversionFailureReason.TypeNotConfirmed, ex.Message);
        Assert.Empty(LeftInWork());
    }
}
