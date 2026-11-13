using System.Text;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// Tables into each other through LibreOffice: CSV, TSV, XLSX, XLS and ODS to any of the five (the same kind included, which changes the
/// separator or the encoding), CSV and TSV to PDF, and plain text to the five as one column.
/// A book that becomes CSV or TSV is written sheet by sheet: to a folder target every sheet is a file named after the input and the sheet;
/// to any other target the sheet of the settings is taken and a note says how many there were.
/// Delimited text is read here (encoding, separator, size) and handed over as UTF-8; formulas of a text input are never evaluated.
/// </summary>
internal sealed class TableEngine(OfficeSettings settings) : IConversionEngine
{
    private const int MaxTextColumns = 1000;

    private static readonly FileFormat[] Books = [FileFormat.Xlsx, FileFormat.Xls, FileFormat.Ods];

    private static readonly FileFormat[] Delimited = [FileFormat.Csv, FileFormat.Tsv];

    private static readonly FileFormat[] Targets = [FileFormat.Csv, FileFormat.Tsv, FileFormat.Xlsx, FileFormat.Xls, FileFormat.Ods];

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> Tools { get; } = [LibreOfficeTools.Soffice];

    /// <inheritdoc />
    public bool SupportsOperations => false;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } = BuildPairs();

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

            string stem = request.InputPath is null ? "table" : Path.GetFileNameWithoutExtension(request.InputPath);
            bool text = request.From.Equals(FileFormat.Csv) || request.From.Equals(FileFormat.Tsv) || request.From.Equals(FileFormat.Text);
            (string input, string? importFilter) = text
                ? await PrepareTextAsync(request, work, stem, cancellationToken).ConfigureAwait(false)
                : (await PlaceBookAsync(request, work, stem, cancellationToken).ConfigureAwait(false), null);
            CheckBookFitsXls(request, input);
            request.ReportProgress(0);

            char delimiter = request.To.Equals(FileFormat.Tsv) ? '\t' : settings.CsvDelimiter;
            bool utf8 = Encodings.IsUtf8(settings.OutputEncoding());
            string convertTo = TableArguments.ConvertTo(request.To, delimiter, settings.CsvQuote, settings.CsvValuesAsShown, utf8 && settings.CsvOutputByteOrderMark);
            IReadOnlyList<string> arguments = TableArguments.Convert(input, output, profile, importFilter, convertTo);
            ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(request.Tool!.Path!, arguments, OnOutputLine: _ => { }), cancellationToken).ConfigureAwait(false);

            string[] produced = [.. new DirectoryInfo(output).GetFiles().Where(file => file.Length > 0).OrderBy(file => file.CreationTimeUtc).ThenBy(file => file.Name, StringComparer.Ordinal).Select(file => file.FullName)];
            if (result.ExitCode != 0 || produced.Length == 0)
            {
                // LibreOffice exits with 0 when it cannot load a file; the missing result is the failure.
                string name = request.InputPath is null ? "таблица" : Path.GetFileName(request.InputPath);
                throw ConversionException.ToolFailed(request.Tool!.Name, result.ExitCode, $"{result.ErrorOutput}\nLibreOffice не смог открыть файл «{name}»".TrimStart());
            }

            if (request.To.Equals(FileFormat.Csv) || request.To.Equals(FileFormat.Tsv))
            {
                await WriteSheetsAsync(request, produced, stem, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await using FileStream file = new(produced[0], FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await file.CopyToAsync(request.Output, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await WorkFolder.RemoveAsync(work).ConfigureAwait(false);
        }
    }

    // Delimited text goes to LibreOffice as UTF-8 with a separator that is known and a size that fits the result.
    private async Task<(string Path, string Filter)> PrepareTextAsync(ConversionRequest request, string work, string stem, CancellationToken cancellationToken)
    {
        await using InputFile file = await request.EnsureInputFileAsync(cancellationToken).ConfigureAwait(false);
        string path = Path.Combine(work, stem + ".csv");
        TableShape shape = await TableText.PrepareAsync(file.Path, path, request.From, settings, cancellationToken).ConfigureAwait(false);
        TableLimits.Check(shape, request.To);
        return (path, TableArguments.Import(shape.Delimiter, settings.CsvQuote, TextColumns(request, shape)));
    }

    // A table that stays text is read column by column as text; the format string is long, so a very wide table is read as numbers, and the note says so.
    private int TextColumns(ConversionRequest request, TableShape shape)
    {
        bool toText = request.To.Equals(FileFormat.Csv) || request.To.Equals(FileFormat.Tsv);
        if (!toText || !settings.CsvKeepValuesAsText)
        {
            return 0;
        }

        if (shape.Columns > MaxTextColumns)
        {
            request.AddNote($"в таблице {shape.Columns} столбцов (больше {MaxTextColumns}): значения прочитаны как числа и даты, ведущие нули могли пропасть");
            return 0;
        }

        return shape.Columns;
    }

    // The real input file is used as it is when its name already carries the right extension; otherwise a copy is made.
    private static async Task<string> PlaceBookAsync(ConversionRequest request, string work, string stem, CancellationToken cancellationToken)
    {
        string extension = "." + request.From.Extension;
        if (request.InputPath is not null && string.Equals(Path.GetExtension(request.InputPath), extension, StringComparison.OrdinalIgnoreCase))
        {
            return request.InputPath;
        }

        string path = Path.Combine(work, stem + extension);
        await using FileStream copy = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await request.Input.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return path;
    }

    // LibreOffice cuts a book that the old XLS cannot hold without a word, so the size is read before the run.
    private static void CheckBookFitsXls(ConversionRequest request, string input)
    {
        if (request.To.Equals(FileFormat.Xls) && BookSize.Measure(input, request.From) is { } size)
        {
            TableLimits.Check(size, request.To);
        }
    }

    private async Task WriteSheetsAsync(ConversionRequest request, string[] sheets, string stem, CancellationToken cancellationToken)
    {
        if (request.OutputFolder is not null)
        {
            foreach (string sheet in sheets)
            {
                string name = Path.GetFileNameWithoutExtension(sheet) + "." + request.To.Extension;
                string path;
                try
                {
                    path = request.GetOutputFilePath(name);
                }
                catch (ArgumentException ex)
                {
                    throw ConversionException.InvalidParameters($"Лист «{SheetName(sheet, stem)}» нельзя записать файлом: {ex.Message}");
                }

                await using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
                await WriteEncodedAsync(sheet, file, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        // Only a book has sheets to choose from; a CSV, TSV or text gives the one file.
        int number = Books.Contains(request.From) ? settings.SheetNumber : 1;
        if (number > sheets.Length)
        {
            throw ConversionException.InvalidParameters($"В книге {sheets.Length} листов, а в настройках выбран лист {settings.SheetNumber} ({nameof(OfficeSettings.SheetNumber)}).");
        }

        string chosen = sheets[number - 1];
        if (sheets.Length > 1)
        {
            request.AddNote($"в книге {sheets.Length} листов, взят лист «{SheetName(chosen, stem)}»");
        }

        await WriteEncodedAsync(chosen, request.Output, cancellationToken).ConfigureAwait(false);
    }

    // LibreOffice names a sheet file "<input>-<sheet>.csv"; a single-sheet text keeps whatever it was given.
    private static string SheetName(string path, string stem)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith(stem + "-", StringComparison.Ordinal) ? name[(stem.Length + 1)..] : name;
    }

    // LibreOffice wrote UTF-8 (with the mark when the settings ask for UTF-8 with it); any other encoding is made here.
    private async Task WriteEncodedAsync(string source, Stream destination, CancellationToken cancellationToken)
    {
        Encoding encoding = settings.OutputEncoding();
        if (Encodings.IsUtf8(encoding))
        {
            await using FileStream file = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await file.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            if (settings.CsvOutputByteOrderMark)
            {
                await destination.WriteAsync(encoding.GetPreamble(), cancellationToken).ConfigureAwait(false);
            }

            Encoder encoder = encoding.GetEncoder();
            using StreamReader reader = new(source, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, 64 * 1024);
            char[] chars = new char[64 * 1024];
            byte[] bytes = new byte[encoding.GetMaxByteCount(chars.Length)];
            int read;
            while ((read = await reader.ReadAsync(chars.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                int count = encoder.GetBytes(chars.AsSpan(0, read), bytes, flush: false);
                await destination.WriteAsync(bytes.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            }

            int tail = encoder.GetBytes([], bytes, flush: true);
            await destination.WriteAsync(bytes.AsMemory(0, tail), cancellationToken).ConfigureAwait(false);
        }
        catch (EncoderFallbackException)
        {
            throw ConversionException.InvalidParameters($"В таблице есть знак, которого нет в кодировке «{settings.CsvOutputEncoding}» ({nameof(OfficeSettings.CsvOutputEncoding)}): выберите UTF-8 или другую кодировку.");
        }
    }

    private static List<ConversionPair> BuildPairs()
    {
        List<ConversionPair> pairs = [];
        foreach (FileFormat from in Books.Concat(Delimited))
        {
            foreach (FileFormat to in Targets)
            {
                pairs.Add(new ConversionPair(from, to) { WritesSeveralFiles = Books.Contains(from) && Delimited.Contains(to) });
            }
        }

        pairs.AddRange(Delimited.Select(from => new ConversionPair(from, FileFormat.Pdf)));
        pairs.AddRange(Targets.Select(to => new ConversionPair(FileFormat.Text, to)));
        return pairs;
    }
}
