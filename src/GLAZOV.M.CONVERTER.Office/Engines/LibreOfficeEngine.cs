using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// Documents, spreadsheets and presentations to PDF through LibreOffice (<c>soffice</c>), an external program.
/// Every run has its own work folder (copy of the input, profile, result) that is removed on success, failure and cancellation.
/// The input goes under a name with the extension of its format, because LibreOffice picks the filter by the name.
/// Text is the one format with two names: a file named <c>.md</c> or <c>.markdown</c> goes as <c>.md</c> so that it is laid out as Markdown,
/// anything else as <c>.txt</c>; the name only hints the layout, it does not prove the type.
/// </summary>
internal sealed class LibreOfficeEngine(OfficeSettings settings) : IConversionEngine
{
    private static readonly FileFormat[] Inputs =
    [
        FileFormat.Docx, FileFormat.Doc, FileFormat.Odt, FileFormat.Rtf,
        FileFormat.Xlsx, FileFormat.Xls, FileFormat.Ods,
        FileFormat.Pptx, FileFormat.Ppt, FileFormat.Odp,
        FileFormat.Html,
        FileFormat.Text,
    ];

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> Tools { get; } = [LibreOfficeTools.Soffice];

    /// <inheritdoc />
    public bool SupportsOperations => false;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } = [.. Inputs.Select(from => new ConversionPair(from, FileFormat.Pdf))];

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string work = Path.Combine(request.TempFolder, $"office-{Guid.NewGuid():N}");
        try
        {
            string output = Path.Combine(work, "out");
            string profile = Path.Combine(work, "profile");
            Directory.CreateDirectory(output);
            LibreOfficeProfile.Write(profile, settings.AllowExternalResources);

            string input = await PlaceInputAsync(request, work, cancellationToken).ConfigureAwait(false);
            request.ReportProgress(0);

            ToolInvocation invocation = Invocation(request.Tool!.Path!, input, output, profile);
            ToolResult result = await ToolRunner.RunAsync(invocation, cancellationToken).ConfigureAwait(false);

            string pdf = Path.Combine(output, Path.GetFileNameWithoutExtension(input) + ".pdf");
            if (result.ExitCode != 0 || !File.Exists(pdf) || new FileInfo(pdf).Length == 0)
            {
                // LibreOffice exits with 0 when it cannot load a file; the missing result is the failure.
                string name = request.InputPath is null ? "документ" : Path.GetFileName(request.InputPath);
                throw ConversionException.ToolFailed(request.Tool!.Name, result.ExitCode, $"{result.ErrorOutput}\nLibreOffice не смог открыть файл «{name}»".TrimStart());
            }

            await using FileStream produced = new(pdf, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await produced.CopyToAsync(request.Output, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await WorkFolder.RemoveAsync(work).ConfigureAwait(false);
        }
    }

    // The real input file is used as it is when its name already carries the right extension; otherwise a copy is made.
    private static async Task<string> PlaceInputAsync(ConversionRequest request, string work, CancellationToken cancellationToken)
    {
        string extension = ExtensionFor(request);
        if (request.InputPath is not null && string.Equals(Path.GetExtension(request.InputPath), extension, StringComparison.OrdinalIgnoreCase))
        {
            return request.InputPath;
        }

        string path = Path.Combine(work, "document" + extension);
        await using FileStream copy = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await request.Input.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return path;
    }

    /// <summary>
    /// What to start. The program prints a progress line to its standard output; the output is taken and dropped,
    /// otherwise those lines would land in the console of the application that uses the library.
    /// </summary>
    internal static ToolInvocation Invocation(string program, string input, string outputFolder, string profileFolder)
    {
        return new ToolInvocation(program, LibreOfficeArguments.ToPdf(input, outputFolder, profileFolder), OnOutputLine: _ => { });
    }

    private static string ExtensionFor(ConversionRequest request)
    {
        if (request.From.Equals(FileFormat.Text) && request.InputPath is not null)
        {
            string name = Path.GetExtension(request.InputPath);
            if (name.Equals(".md", StringComparison.OrdinalIgnoreCase) || name.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
            {
                return ".md";
            }
        }

        return "." + request.From.Extension;
    }
}
