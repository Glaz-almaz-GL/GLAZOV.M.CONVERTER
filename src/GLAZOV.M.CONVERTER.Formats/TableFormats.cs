using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// The delimited-text table formats of the standard set. Spreadsheet books (XLSX, XLS, ODS) are named by <see cref="DocumentFormats"/>.
/// </summary>
public static class TableFormats
{
    /// <summary>
    /// CSV: rows of fields separated by a comma or a semicolon.
    /// </summary>
    public static FileFormat Csv { get; } = FileFormat.Create("CSV", "text/csv", "csv");

    /// <summary>
    /// TSV: rows of fields separated by a tab.
    /// </summary>
    public static FileFormat Tsv { get; } = FileFormat.Create("TSV", "text/tab-separated-values", "tsv");
}
