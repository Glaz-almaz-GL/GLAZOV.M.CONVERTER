using GLAZOV.M.CONVERTER.Office.Engines;
using System.Text;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Office.Tests.Live;

/// <summary>
/// Real document files for the live run, made by the real LibreOffice from flat ODF sources: a text, a workbook and a presentation
/// in every format of the catalog, plus HTML, Markdown, plain text in three encodings, a hundred-page text, a cut-off file,
/// a picture under the name of a document, and thirty small documents for the folder run.
/// </summary>
public sealed class DocumentRunFixture : IAsyncLifetime
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "convdocrun-" + Guid.NewGuid().ToString("N"));

    public string Soffice { get; private set; } = string.Empty;

    /// <summary>The inputs of the main run: label, path, kind (text, sheet, slides, page, plain).</summary>
    public IReadOnlyList<(string Label, string Path, string Kind)> Inputs { get; private set; } = [];

    public string Big => Path.Combine(Folder, "большой.docx");

    public string Broken => Path.Combine(Folder, "обрезанный.docx");

    public string Liar => Path.Combine(Folder, "лжец.docx");

    public string BatchFolder => Path.Combine(Folder, "пакет");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Folder);
        Soffice = new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(LibreOfficeTools.Soffice).Path
            ?? throw new InvalidOperationException("LibreOffice не найден.");

        string sources = Path.Combine(Folder, "исходники");
        Directory.CreateDirectory(sources);
        string text = await WriteAsync(sources, "отчёт.fodt", DocumentSamples.Text());
        string sheet = await WriteAsync(sources, "таблица.fods", DocumentSamples.Sheet());
        string slides = await WriteAsync(sources, "презентация.fodp", DocumentSamples.Slides());

        List<(string, string, string)> inputs = [];
        foreach (string format in new[] { "docx", "doc", "odt", "rtf" })
        {
            inputs.Add((format, await ConvertAsync(text, format, $"отчёт.{format}"), "text"));
        }

        foreach (string format in new[] { "xlsx", "xls", "ods" })
        {
            inputs.Add((format, await ConvertAsync(sheet, format, $"таблица.{format}"), "sheet"));
        }

        foreach (string format in new[] { "pptx", "ppt", "odp" })
        {
            inputs.Add((format, await ConvertAsync(slides, format, $"презентация.{format}"), "slides"));
        }

        string html = Path.Combine(Folder, "страница.html");
        await File.WriteAllTextAsync(html, DocumentSamples.Html, new UTF8Encoding(false));
        inputs.Add(("html", html, "page"));

        string markdown = Path.Combine(Folder, "записка.md");
        await File.WriteAllTextAsync(markdown, DocumentSamples.Markdown, new UTF8Encoding(false));
        inputs.Add(("md", markdown, "markdown"));

        const string plain = "Простой текст: съешь ещё этих мягких французских булок, да выпей чаю.\nВторая строка с цифрами 12345.\n";
        foreach ((string label, Encoding encoding) in new (string, Encoding)[] { ("txt-utf8", new UTF8Encoding(false)), ("txt-utf8-bom", new UTF8Encoding(true)), ("txt-utf16", new UnicodeEncoding(false, true)) })
        {
            string path = Path.Combine(Folder, $"текст-{label}.txt");
            await File.WriteAllBytesAsync(path, [.. encoding.GetPreamble(), .. encoding.GetBytes(plain)]);
            inputs.Add((label, path, "plain"));
        }

        Inputs = inputs;

        string big = await WriteAsync(sources, "большой.fodt", DocumentSamples.Text(extraPages: 96));
        File.Move(await ConvertAsync(big, "docx", "большой.docx"), Big, overwrite: true);

        byte[] whole = await File.ReadAllBytesAsync(inputs[0].Item2);
        await File.WriteAllBytesAsync(Broken, whole.AsMemory(0, whole.Length / 2).ToArray());
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Samples", "sample.png"), Liar, overwrite: true);

        Directory.CreateDirectory(BatchFolder);
        string[] kinds = ["docx", "odt", "xlsx", "ods", "pptx", "odp"];
        for (int i = 0; i < 30; i++)
        {
            string kind = kinds[i % kinds.Length];
            File.Copy(inputs.First(x => x.Item1 == kind).Item2, Path.Combine(BatchFolder, $"документ{i:00}.{kind}"));
        }
    }

    public Task DisposeAsync()
    {
        // The sources go to the folder of the owner's run too, so a result can be set beside the file it came from.
        string? keep = Environment.GetEnvironmentVariable("CONVERTER_RUN_KEEP");
        if (!string.IsNullOrWhiteSpace(keep))
        {
            string folder = Path.Combine(keep, "documents", "исходники");
            Directory.CreateDirectory(folder);
            foreach ((_, string path, _) in Inputs)
            {
                File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), overwrite: true);
            }
        }

        Directory.Delete(Folder, recursive: true);
        return Task.CompletedTask;
    }

    private static async Task<string> WriteAsync(string folder, string name, string content)
    {
        string path = Path.Combine(folder, name);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        return path;
    }

    // One program run with a profile of its own, then the result is moved to its final name next to the sources.
    private async Task<string> ConvertAsync(string source, string format, string finalName)
    {
        string outFolder = Path.Combine(Folder, "сделано");
        Directory.CreateDirectory(outFolder);
        string profile = Path.Combine(Folder, "профиль-" + Guid.NewGuid().ToString("N"));
        string filter = format == "txt" ? "txt:Text" : format;

        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Soffice,
            [$"-env:UserInstallation={new Uri(profile).AbsoluteUri}", "--headless", "--norestore", "--convert-to", filter, "--outdir", outFolder, source]));

        string made = Path.Combine(outFolder, Path.GetFileNameWithoutExtension(source) + "." + format);
        Assert.True(result.ExitCode == 0 && File.Exists(made), $"LibreOffice не сделал {format}: {result.ErrorOutput}");
        string target = Path.Combine(Folder, finalName);
        File.Move(made, target, overwrite: true);
        Directory.Delete(profile, recursive: true);
        return target;
    }
}
