using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Tables;

/// <summary>
/// Delimited-text tables by content: TSV, then CSV. It has no label of its own, so it has to be connected before the text detector,
/// which would name the same data plain text.
/// </summary>
internal sealed class TableDetector() : FamilyDetector(
[
    (FileFormat.Tsv, TsvSignature.Matches),
    (FileFormat.Csv, CsvSignature.Matches),
]), IFormatDetector
{
    /// <inheritdoc />
    public bool ByContent => true;
}
