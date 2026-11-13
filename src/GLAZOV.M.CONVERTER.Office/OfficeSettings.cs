using System.Text;
using GLAZOV.M.CONVERTER.Office.Engines;

namespace GLAZOV.M.CONVERTER.Office;

/// <summary>
/// Settings of the "Office" extension. Pass them to <see cref="OfficeExtension(OfficeSettings)"/>; wrong values are refused there.
/// </summary>
public sealed class OfficeSettings
{
    /// <summary>
    /// Whether a document may load pictures, style sheets and backgrounds from outside itself: web addresses and linked files.
    /// Off by default: a document is untrusted input, and what it cannot load is missing from the PDF without an error.
    /// Turn it on only for documents you trust, for example internal reports with pictures on the company server.
    /// Macros never run, whatever this value is.
    /// </summary>
    public bool AllowExternalResources { get; set; }

    /// <summary>
    /// Field separator of CSV, in and out. A CSV input whose first row has no such separator but has a semicolon or a comma is read by that one.
    /// A TSV always uses the tab. Default: comma.
    /// </summary>
    public char CsvDelimiter { get; set; } = ',';

    /// <summary>
    /// Text delimiter (quote) of CSV and TSV, in and out. Default: double quote.
    /// </summary>
    public char CsvQuote { get; set; } = '"';

    /// <summary>
    /// Encoding of the CSV and TSV files that are written, by its name (for example <c>utf-8</c>, <c>utf-16</c>, <c>windows-1251</c>).
    /// A character the encoding cannot hold fails the conversion with the reason InvalidParameters. Default: <c>utf-8</c>.
    /// </summary>
    public string CsvOutputEncoding { get; set; } = "utf-8";

    /// <summary>
    /// Whether the written CSV and TSV start with a byte order mark; it has no effect on single-byte encodings.
    /// On by default so that Excel reads Cyrillic in UTF-8 correctly.
    /// </summary>
    public bool CsvOutputByteOrderMark { get; set; } = true;

    /// <summary>
    /// Encoding of a CSV, TSV or text input that has no byte order mark and is not valid UTF-8, by its name. Default: <c>windows-1251</c>.
    /// </summary>
    public string CsvInputEncoding { get; set; } = "windows-1251";

    /// <summary>
    /// <c>true</c>: numbers and dates are written to CSV as they look in the sheet; <c>false</c>: as they are stored. Default: as shown.
    /// </summary>
    public bool CsvValuesAsShown { get; set; } = true;

    /// <summary>
    /// <c>true</c>: when a CSV, TSV or text becomes CSV or TSV, every value is read as text and written back unchanged, so <c>007</c> and <c>1e3</c> stay as they are.
    /// <c>false</c>: values are read as numbers and dates, the way a spreadsheet opens them. A table of more than 1000 columns is read as numbers either way, with a note.
    /// A table that becomes a book or a PDF is always read as numbers and dates. Default: <c>true</c>.
    /// </summary>
    public bool CsvKeepValuesAsText { get; set; } = true;

    /// <summary>
    /// The sheet (counted from 1) that is written when a book becomes one CSV or TSV file (a target that is a file, a stream or memory).
    /// A folder target gets every sheet. A book with fewer sheets fails the conversion with the reason InvalidParameters. Default: 1.
    /// </summary>
    public int SheetNumber { get; set; } = 1;

    internal OfficeSettings Copy()
    {
        Validate();
        return new OfficeSettings
        {
            AllowExternalResources = AllowExternalResources,
            CsvDelimiter = CsvDelimiter,
            CsvQuote = CsvQuote,
            CsvOutputEncoding = CsvOutputEncoding,
            CsvOutputByteOrderMark = CsvOutputByteOrderMark,
            CsvInputEncoding = CsvInputEncoding,
            CsvValuesAsShown = CsvValuesAsShown,
            CsvKeepValuesAsText = CsvKeepValuesAsText,
            SheetNumber = SheetNumber,
        };
    }

    internal Encoding OutputEncoding()
    {
        return Encodings.Find(CsvOutputEncoding, nameof(CsvOutputEncoding));
    }

    internal Encoding InputEncoding()
    {
        return Encodings.Find(CsvInputEncoding, nameof(CsvInputEncoding));
    }

    private void Validate()
    {
        if (CsvDelimiter is '\r' or '\n' or '\0' || CsvDelimiter == CsvQuote)
        {
            throw new ArgumentException("CsvDelimiter must not be a line break or zero and must differ from CsvQuote.", nameof(CsvDelimiter));
        }

        if (CsvQuote is '\r' or '\n' or '\0')
        {
            throw new ArgumentException("CsvQuote must not be a line break or zero.", nameof(CsvQuote));
        }

        if (SheetNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SheetNumber), SheetNumber, "SheetNumber counts from 1.");
        }

        _ = OutputEncoding();
        _ = InputEncoding();
    }
}
