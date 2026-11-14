using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;
using System.IO.Compression;
using System.Xml;

namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// The largest sheet of an XLSX or ODS book, read from the file without LibreOffice: rows with a value and columns with a value.
/// Used to refuse a book that the old XLS cannot hold, because LibreOffice cuts such a book without a word.
/// </summary>
internal static class BookSize
{
    private const string TableNamespace = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private const string OfficeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    /// <summary>
    /// The size of the largest sheet; <c>null</c> when the format is not read here or the file does not show it.
    /// </summary>
    /// <param name="path">The book file.</param>
    /// <param name="format">XLSX or ODS.</param>
    internal static TableShape? Measure(string path, FileFormat format)
    {
        try
        {
            using ZipArchive zip = ZipFile.OpenRead(path);
            if (format.Equals(FileFormat.Xlsx))
            {
                // XLSX
                return MeasureXlsx(zip);
            }

            return format.Equals(FileFormat.Ods) ? MeasureOds(zip) : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Names of the sheets of an XLSX or ODS book in their workbook order; empty when not read here.
    /// </summary>
    /// <param name="path">The book file.</param>
    /// <param name="format">XLSX or ODS.</param>
    internal static IReadOnlyList<string> SheetNames(string path, FileFormat format)
    {
        try
        {
            using ZipArchive zip = ZipFile.OpenRead(path);
            if (format.Equals(FileFormat.Xlsx))
            {
                // XLSX
                return ReadXlsxSheetNames(zip);
            }

            return format.Equals(FileFormat.Ods) ? ReadOdsSheetNames(zip) : [];
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException)
        {
            return [];
        }
    }

    private static List<string> ReadXlsxSheetNames(ZipArchive zip)
    {
        ZipArchiveEntry? workbook = zip.GetEntry("xl/workbook.xml");
        if (workbook is null)
        {
            return [];
        }

        List<string> names = [];
        using XmlReader reader = XmlReader.Create(
            workbook.Open(),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet")
            {
                string? name = reader.GetAttribute("name");
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    private static List<string> ReadOdsSheetNames(ZipArchive zip)
    {
        ZipArchiveEntry? content = zip.GetEntry("content.xml");
        if (content is null)
        {
            return [];
        }

        List<string> names = [];
        using XmlReader reader = XmlReader.Create(
            content.Open(),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

        while (reader.Read())
        {
            if (IsTableElement(reader, "table"))
            {
                string? name = reader.GetAttribute("name", TableNamespace);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    // The dimension element of each sheet names its corner cell, for example A1:C70000.
    private static TableShape? MeasureXlsx(ZipArchive zip)
    {
        long rows = 0;
        int columns = 0;
        foreach (ZipArchiveEntry entry in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using XmlReader reader = XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "dimension")
                {
                    (long sheetRows, int sheetColumns) = Corner(reader.GetAttribute("ref"));
                    rows = Math.Max(rows, sheetRows);
                    columns = Math.Max(columns, sheetColumns);
                    break;
                }

                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheetData")
                {
                    break;
                }
            }
        }

        return new TableShape(',', rows, columns);
    }

    private static (long Rows, int Columns) Corner(string? reference)
    {
        string last = (reference ?? string.Empty).Split(':')[^1];
        int letters = 0;
        int column = 0;
        while (letters < last.Length && char.IsAsciiLetter(last[letters]))
        {
            column = (column * 26) + char.ToUpperInvariant(last[letters]) - 'A' + 1;
            letters++;
        }

        return long.TryParse(last[letters..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long row) ? (row, column) : (0, 0);
    }

    private static TableShape? MeasureOds(ZipArchive zip)
    {
        ZipArchiveEntry? content = zip.GetEntry("content.xml");
        if (content is null)
        {
            return null;
        }

        long maxRows = 0;
        int maxColumns = 0;
        long row = 0;
        long sheetRows = 0;

        using XmlReader reader = XmlReader.Create(
            content.Open(),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

        while (reader.Read())
        {
            if (IsTableElement(reader, "table"))
            {
                maxRows = Math.Max(maxRows, sheetRows);
                row = 0;
                sheetRows = 0;
                continue;
            }

            if (!IsTableElement(reader, "table-row"))
            {
                continue;
            }

            long repeat = Repeat(reader.GetAttribute("number-rows-repeated", TableNamespace));
            int widest = MeasureRowWidth(reader);

            row += repeat;
            if (widest > 0)
            {
                sheetRows = row;
                maxColumns = Math.Max(maxColumns, widest);
            }
        }

        return new TableShape(',', Math.Max(maxRows, sheetRows), maxColumns);
    }

    private static int MeasureRowWidth(XmlReader rowReader)
    {
        if (rowReader.IsEmptyElement)
        {
            return 0;
        }

        int column = 0;
        int widest = 0;

        using XmlReader cells = rowReader.ReadSubtree();
        cells.Read();

        while (cells.Read())
        {
            if (!IsCellElement(cells))
            {
                continue;
            }

            long repeated = Repeat(cells.GetAttribute("number-columns-repeated", TableNamespace));
            column += (int)Math.Min(repeated, int.MaxValue / 2);

            if (cells.GetAttribute("value-type", OfficeNamespace) is not null)
            {
                widest = column;
            }
        }

        return widest;
    }

    private static bool IsTableElement(XmlReader reader, string localName)
    {
        return reader.NodeType == XmlNodeType.Element &&
        reader.NamespaceURI == TableNamespace &&
        reader.LocalName == localName;
    }

    private static bool IsCellElement(XmlReader reader)
    {
        return IsTableElement(reader, "table-cell") ||
        IsTableElement(reader, "covered-table-cell");
    }

    private static long Repeat(string? value)
    {
        return long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long repeat) && repeat > 0 ? repeat : 1;
    }
}
