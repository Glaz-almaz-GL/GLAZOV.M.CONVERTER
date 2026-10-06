using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection.Documents;

/// <summary>
/// XLSX: Excel workbook in the Office Open XML package, told by what the archive holds inside the window.
/// </summary>
internal static class XlsxSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return ZipPackage.Kind(head)?.Equals(FileFormat.Xlsx) == true;
    }
}
