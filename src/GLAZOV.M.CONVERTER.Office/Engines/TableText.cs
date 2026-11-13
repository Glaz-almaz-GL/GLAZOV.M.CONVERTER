using System.Text;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;

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
    /// The separator to read with: the tab for TSV, the one of the settings for text, and for CSV the one of the settings
    /// when the first row has it outside quotes, otherwise a semicolon or a comma that the first row has.
    /// </summary>
    internal static char Choose(string firstRecord, FileFormat from, char configured, char quote)
    {
        if (from.Equals(FileFormat.Tsv))
        {
            return '\t';
        }

        if (!from.Equals(FileFormat.Csv))
        {
            return configured;
        }

        foreach (char candidate in new[] { configured, ';', ',' }.Distinct())
        {
            if (candidate != quote && CountOutsideQuotes(firstRecord, candidate, quote) > 0)
            {
                return candidate;
            }
        }

        return configured;
    }

    /// <summary>
    /// Rows and the widest row of a text read with the given separator and quote.
    /// </summary>
    internal static async Task<TableShape> MeasureAsync(string path, char delimiter, char quote, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, BufferSize);
        char[] buffer = new char[BufferSize];
        long rows = 0;
        int columns = 0;
        int fields = 1;
        bool inQuotes = false;
        bool pending = false;
        bool lastWasCarriageReturn = false;

        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];
                if (lastWasCarriageReturn)
                {
                    lastWasCarriageReturn = false;
                    if (c == '\n')
                    {
                        continue;
                    }
                }

                if (c == quote)
                {
                    inQuotes = !inQuotes;
                    pending = true;
                }
                else if (!inQuotes && (c == '\n' || c == '\r'))
                {
                    rows++;
                    columns = Math.Max(columns, fields);
                    fields = 1;
                    pending = false;
                    lastWasCarriageReturn = c == '\r';
                }
                else
                {
                    pending = true;
                    if (!inQuotes && c == delimiter)
                    {
                        fields++;
                    }
                }
            }
        }

        if (pending)
        {
            rows++;
            columns = Math.Max(columns, fields);
        }

        return new TableShape(delimiter, rows, columns);
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
}
