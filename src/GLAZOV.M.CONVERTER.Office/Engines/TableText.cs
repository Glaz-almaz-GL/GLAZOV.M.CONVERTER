using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using System.Text;

namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// Reads a CSV, TSV or text input: turns it into UTF-8 (byte order mark, else valid UTF-8, else the encoding of the settings),
/// picks the field separator and measures the table. LibreOffice then gets plain UTF-8 and a known separator.
/// </summary>
internal static class TableText
{
    private const int BufferSize = 64 * 1024;
    private const int FirstRecordLimit = 1024 * 1024;

    /// <summary>
    /// Writes the text of <paramref name="inputPath"/> to <paramref name="outputPath"/> as UTF-8 without a mark and returns its shape.
    /// </summary>
    /// <param name="inputPath">The input file.</param>
    /// <param name="outputPath">Where the UTF-8 copy goes.</param>
    /// <param name="from">Format of the input: CSV, TSV or text.</param>
    /// <param name="settings">Separator, quote and the fallback encoding.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    internal static async Task<TableShape> PrepareAsync(string inputPath, string outputPath, FileFormat from, OfficeSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await CopyAsync(inputPath, outputPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), cancellationToken).ConfigureAwait(false);
        }
        catch (DecoderFallbackException)
        {
            await ConvertFromFallbackAsync(inputPath, outputPath, settings, cancellationToken).ConfigureAwait(false);
        }

        char delimiter = await ChooseDelimiterAsync(outputPath, from, settings, cancellationToken).ConfigureAwait(false);
        return await MeasureAsync(outputPath, delimiter, settings.CsvQuote, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The separator to read with: the tab for TSV, the one of the settings for text, and for CSV the one of the
    /// settings when the first row has it outside quotes, otherwise a semicolon or a comma that the first row has.
    /// </summary>
    internal static char Choose(string firstRecord, FileFormat from, char configured, char quote)
    {
        if (from.Equals(FileFormat.Tsv))
        {
            // TSG detected
            return '\t';
        }

        return !from.Equals(FileFormat.Csv)
            ? configured
            : new[] { configured, ';', ',' }
            .Distinct()
            .Where(candidate => candidate != quote && CountOutsideQuotes(firstRecord, candidate, quote) > 0)
            .DefaultIfEmpty(configured)
            .First();
    }


    /// <summary>
    /// Rows and the widest row of a text read with the given separator and quote.
    /// </summary>
    internal static async Task<TableShape> MeasureAsync(
     string path,
     char delimiter,
     char quote,
     CancellationToken cancellationToken = default)
    {
        using StreamReader reader = new(
            path,
            new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks: false,
            BufferSize);

        char[] buffer = new char[BufferSize];
        MeasureState state = new(delimiter, quote);
        int read;

        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            state.ProcessChunk(buffer.AsSpan(0, read));
        }

        return state.ToTableShape();
    }

    private static async Task<char> ChooseDelimiterAsync(string path, FileFormat from, OfficeSettings settings, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, BufferSize);
        StringBuilder record = new();
        char[] buffer = new char[BufferSize];
        bool inQuotes = false;
        int read;
        while (record.Length < FirstRecordLimit && (read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];
                if (c == settings.CsvQuote)
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && c is '\n' or '\r')
                {
                    return Choose(record.ToString(), from, settings.CsvDelimiter, settings.CsvQuote);
                }

                record.Append(c);
            }
        }

        return Choose(record.ToString(), from, settings.CsvDelimiter, settings.CsvQuote);
    }

    private static int CountOutsideQuotes(string record, char delimiter, char quote)
    {
        int count = 0;
        bool inQuotes = false;
        foreach (char c in record)
        {
            if (c == quote)
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && c == delimiter)
            {
                count++;
            }
        }

        return count;
    }

    private static async Task ConvertFromFallbackAsync(string inputPath, string outputPath, OfficeSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await CopyAsync(inputPath, outputPath, settings.InputEncoding(), cancellationToken).ConfigureAwait(false);
        }
        catch (DecoderFallbackException)
        {
            throw ConversionException.InvalidParameters($"Текст не читается ни как UTF-8, ни в кодировке «{settings.CsvInputEncoding}» из настроек ({nameof(OfficeSettings.CsvInputEncoding)}): укажите кодировку файла.");
        }
    }

    // A mark at the start of the file decides the encoding; without one the given encoding is used, and bad bytes throw.
    private static async Task CopyAsync(string inputPath, string outputPath, Encoding encoding, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(inputPath, encoding, detectEncodingFromByteOrderMarks: true, BufferSize);
        await using StreamWriter writer = new(outputPath, append: false, new UTF8Encoding(false), BufferSize);
        char[] buffer = new char[BufferSize];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await writer.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private struct MeasureState(char delimiter, char quote)
    {
        private long _rows = 0;
        private int _columns = 0;
        private int _fields = 1;
        private bool _inQuotes = false;
        private bool _pending = false;
        private bool _lastWasCarriageReturn = false;

        public void ProcessChunk(ReadOnlySpan<char> chunk)
        {
            foreach (char c in chunk)
            {
                ProcessChar(c);
            }
        }

        private void ProcessChar(char c)
        {
            if (ShouldSkipLineFeed(c))
            {
                return;
            }

            if (c == quote)
            {
                _inQuotes = !_inQuotes;
                _pending = true;
                return;
            }

            if (!_inQuotes && IsLineBreak(c))
            {
                CompleteRow(c == '\r');
                return;
            }

            _pending = true;
            if (!_inQuotes && c == delimiter)
            {
                _fields++;
            }
        }

        private bool ShouldSkipLineFeed(char c)
        {
            if (!_lastWasCarriageReturn)
            {
                return false;
            }

            _lastWasCarriageReturn = false;
            return c == '\n';
        }

        private static bool IsLineBreak(char c)
        {
            return c is '\n' or '\r';
        }

        private void CompleteRow(bool isCarriageReturn)
        {
            _rows++;
            _columns = Math.Max(_columns, _fields);
            _fields = 1;
            _pending = false;
            _lastWasCarriageReturn = isCarriageReturn;
        }

        public readonly TableShape ToTableShape()
        {
            long finalRows = _pending ? _rows + 1 : _rows;
            int finalColumns = _pending ? Math.Max(_columns, _fields) : _columns;
            return new TableShape(delimiter, finalRows, finalColumns);
        }
    }

}
