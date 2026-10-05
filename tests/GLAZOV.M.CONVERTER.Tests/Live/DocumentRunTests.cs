using System.Diagnostics;
using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;
using UglyToad.PdfPig;

namespace GLAZOV.M.CONVERTER.Tests.Live;

/// <summary>
/// Run of documents on real files through the public calls and the real LibreOffice: every input format of the catalog
/// file to file, a stream without rewinding, memory, a folder of thirty in parallel at one and at four, cancellation,
/// a cut-off file, a picture under the name of a document, a hundred pages (time and memory), progress.
/// Results are read back with PdfPig. When the variable CONVERTER_RUN_KEEP names a folder, the results and the figures
/// are kept in its subfolder "documents" for the owner to look at. Files made by Word, Excel and PowerPoint are not
/// among the inputs; Linux and macOS are not checked: everything here ran on Windows.
/// </summary>
[Collection("LibreOffice")]
public sealed class DocumentRunTests(DocumentRunFixture fixture) : IClassFixture<DocumentRunFixture>, IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "convrun-docs-out-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private string Out(string name)
    {
        Directory.CreateDirectory(_work);
        return Path.Combine(_work, name);
    }

    private static string? KeepFolder()
    {
        string? keep = Environment.GetEnvironmentVariable("CONVERTER_RUN_KEEP");
        if (string.IsNullOrWhiteSpace(keep))
        {
            return null;
        }

        string folder = Path.Combine(keep, "documents");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void Keep(string produced, string label)
    {
        string? folder = KeepFolder();
        if (folder is not null)
        {
            File.Copy(produced, Path.Combine(folder, label + Path.GetExtension(produced)), overwrite: true);
        }
    }

    private static void Note(string line)
    {
        string? folder = KeepFolder();
        if (folder is not null)
        {
            File.AppendAllText(Path.Combine(folder, "итоги.txt"), line + Environment.NewLine);
        }
    }

    private static (int Pages, string Text, int Images) Read(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        return (document.NumberOfPages, string.Join('\n', document.GetPages().Select(page => page.Text)), document.GetPages().Sum(page => page.GetImages().Count()));
    }

    private static void AssertContent(string kind, string label, (int Pages, string Text, int Images) pdf)
    {
        string where = $" ({label})";
        switch (kind)
        {
            case "text":
                Assert.True(pdf.Pages >= 4, "pages " + pdf.Pages + where);
                Assert.True(pdf.Text.Contains("Отчёт"), "no \"Отчёт\"" + where + ": " + pdf.Text);
                Assert.True(pdf.Text.Contains("колонтитул"), "no \"колонтитул\"" + where + ": " + pdf.Text);
                Assert.True(pdf.Text.Contains("Контракт"), "no \"Контракт\"" + where + ": " + pdf.Text);
                Assert.True(pdf.Text.Contains("мягких"), "no \"мягких\"" + where + ": " + pdf.Text);
                Assert.True(pdf.Images >= 1, "no picture" + where);
                break;
            case "sheet":
                Assert.True(pdf.Pages >= 2, "pages " + pdf.Pages + where);
                Assert.True(pdf.Text.Contains("Яблоки"), "no \"Яблоки\"" + where + ": " + pdf.Text);
                Assert.True(pdf.Text.Contains("Груши"), "no \"Груши\"" + where + ": " + pdf.Text);
                Assert.True(pdf.Text.Contains("855"), "no \"855\"" + where + ": " + pdf.Text);
                Assert.True(pdf.Text.Contains("Сумма"), "no \"Сумма\"" + where + ": " + pdf.Text);
                break;
            case "slides":
                Assert.Equal(3, pdf.Pages);
                Assert.True(pdf.Text.Contains("Слайд третий"), "no \"Слайд третий\"" + where + ": " + pdf.Text);
                break;
            case "page":
                Assert.True(pdf.Text.Contains("Заголовок страницы"), "no \"Заголовок страницы\"" + where + ": " + pdf.Text);
                Assert.True(pdf.Text.Contains("Правая ячейка"), "no \"Правая ячейка\"" + where + ": " + pdf.Text);
                break;
            case "markdown":
                Assert.True(pdf.Text.Contains("Заголовок записки"), "no \"Заголовок записки\"" + where + ": " + pdf.Text);
                Assert.False(pdf.Text.Contains("#"), "has \"#\"" + where + ": " + pdf.Text);
                Assert.False(pdf.Text.Contains("**"), "has \"**\"" + where + ": " + pdf.Text);
                break;
            default:
                Assert.True(pdf.Text.Contains("съешь ещё этих мягких"), "no \"съешь ещё этих мягких\"" + where + ": " + pdf.Text);
                break;
        }
    }

    [OfficeFact]
    public async Task EveryInputFormat_File_ToFile_ReadsBack()
    {
        Converter converter = new(new ConverterSettings());
        int done = 0;

        foreach ((string label, string path, string kind) in fixture.Inputs)
        {
            string output = Out($"{label}.pdf");

            ConversionResult result = await converter.ConvertAsync(Source.File(path), Target.File(output, FileFormat.Pdf));

            Assert.True(result.Succeeded, label);
            Assert.Same(FileFormat.Pdf, await FileTypeDetector.DetectAsync(Source.File(output)));
            AssertContent(kind, label, Read(await File.ReadAllBytesAsync(output)));
            Keep(output, $"{Path.GetFileNameWithoutExtension(path)}-{label}");
            done++;
        }

        Assert.Equal(fixture.Inputs.Count, done);
        Assert.Equal(15, done);
    }

    [OfficeFact]
    public async Task StreamWithoutRewinding_AndMemoryResult_AgreeWithTheFile()
    {
        Converter converter = new();

        foreach ((string label, string path, string kind) in fixture.Inputs.Where(i => i.Label is "docx" or "xls" or "odp" or "html" or "txt-utf16"))
        {
            await using NonSeekableFile source = new(path);

            ConversionResult result = await converter.ConvertAsync(Source.Stream(source), Target.Memory(FileFormat.Pdf));

            Assert.True(result.Succeeded, label);
            Assert.NotNull(result.Bytes);
            AssertContent(kind, label, Read(result.Bytes!));
        }
    }

    [OfficeFact]
    public async Task Folder_To_Folder_ThirtyDocuments_ParallelismNeverExceedsTheSetting()
    {
        foreach (int limit in new[] { 1, 4 })
        {
            string target = Out($"пакет-{limit}");
            Converter converter = new(new ConverterSettings { MaxParallelTasks = limit });
            using PeakWatcher watcher = new("soffice.bin");
            Stopwatch clock = Stopwatch.StartNew();

            BatchReport report = await converter.ConvertBatchAsync(Source.Folder(fixture.BatchFolder), Target.Folder(target, FileFormat.Pdf));

            clock.Stop();
            Assert.Equal(30, report.Total);
            Assert.Equal(30, report.Succeeded);
            Assert.Equal(30, Directory.GetFiles(target, "*.pdf").Length);
            Assert.True(watcher.Peak <= limit, $"At limit {limit} there were {watcher.Peak} soffice.bin processes at once.");
            if (limit == 4)
            {
                Assert.True(watcher.Peak >= 2, $"At limit 4 no parallelism is seen: peak {watcher.Peak}.");
            }

            Note($"Папка из 30 документов, MaxParallelTasks {limit}: {clock.Elapsed.TotalSeconds:F1} с, одновременно soffice.bin не больше {watcher.Peak}, память пик {watcher.PeakMemory / 1048576} МБ.");
        }
    }

    [OfficeFact]
    public async Task Cancel_InTheMiddleOfAHundredPages_StopsTheProgram_AndLeavesNothing()
    {
        string temp = Out("временная");
        Directory.CreateDirectory(temp);
        Converter converter = new(new ConverterSettings { TempFolder = temp });
        using CancellationTokenSource cts = new();
        string target = Out("отменённое.pdf");

        Task<ConversionResult> run = converter.ConvertAsync(Source.File(fixture.Big), Target.File(target, FileFormat.Pdf), cancellationToken: cts.Token);
        await Task.Delay(1500);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await Task.Delay(1500);
        Assert.Empty(Process.GetProcessesByName("soffice.bin"));
        Assert.Empty(Process.GetProcessesByName("soffice"));
        Assert.Empty(Directory.GetFileSystemEntries(temp));
        Assert.False(File.Exists(target));
    }

    [OfficeFact]
    public async Task Broken_AndLiar_AreHonestlyRefused()
    {
        Converter converter = new();

        ConversionException broken = await Assert.ThrowsAnyAsync<ConversionException>(() =>
            converter.ConvertAsync(Source.File(fixture.Broken), Target.File(Out("никогда.pdf"), FileFormat.Pdf)));
        ConversionException liar = await Assert.ThrowsAsync<ConversionException>(() =>
            converter.ConvertAsync(Source.File(fixture.Liar), Target.File(Out("лжец.pdf"), FileFormat.Pdf)));

        Assert.True(broken.Reason is ConversionFailureReason.TypeNotConfirmed or ConversionFailureReason.ToolFailed, broken.Message);
        Assert.False(File.Exists(Out("никогда.pdf")));
        // A picture under the name .docx is a picture: the converter of documents does not take it.
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, liar.Reason);
        Assert.Contains("PNG", liar.Message);
        Assert.False(File.Exists(Out("лжец.pdf")));
    }

    [OfficeFact]
    public async Task AHundredPages_TimeAndMemory_AreWithinReason()
    {
        string output = Out("большой.pdf");
        using PeakWatcher watcher = new("soffice.bin");
        Stopwatch clock = Stopwatch.StartNew();

        ConversionResult result = await new Converter().ConvertAsync(Source.File(fixture.Big), Target.File(output, FileFormat.Pdf));

        clock.Stop();
        Assert.True(result.Succeeded);
        (int pages, _, _) = Read(await File.ReadAllBytesAsync(output));
        Assert.True(pages >= 100, $"pages {pages}");
        Assert.True(clock.Elapsed < TimeSpan.FromMinutes(2), $"{clock.Elapsed.TotalSeconds:F0} s");
        Note($"Документ в {pages} страниц: {clock.Elapsed.TotalSeconds:F1} с, память пик {watcher.PeakMemory / 1048576} МБ, размер PDF {new FileInfo(output).Length / 1024} КБ.");
        Keep(output, "большой-100-страниц");
    }

    [OfficeFact]
    public async Task Progress_StartsAtZero_AndEndsAt100()
    {
        List<double> percents = [];
        IProgress<ConversionProgress> progress = new InlineProgress<ConversionProgress>(p => percents.Add(p.Percent));

        await new Converter().ConvertAsync(Source.File(fixture.Inputs[0].Path), Target.Memory(FileFormat.Pdf), progress: progress);

        Assert.Equal(0d, percents[0]);
        Assert.Equal(100d, percents[^1]);
        Assert.Equal(percents.Order().ToList(), percents);
    }
}
