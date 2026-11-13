namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// What is known about a delimited text after it was read: the separator used, the number of rows and the widest row.
/// </summary>
/// <param name="Delimiter">Field separator of the text.</param>
/// <param name="Rows">Rows (records), empty ones included.</param>
/// <param name="Columns">Fields in the widest row.</param>
internal readonly record struct TableShape(char Delimiter, long Rows, int Columns);
