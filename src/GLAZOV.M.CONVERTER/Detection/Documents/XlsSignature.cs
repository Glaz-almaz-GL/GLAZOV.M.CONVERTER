using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Detection.Documents;

/// <summary>
/// XLS: Excel workbook in a compound file (stream Workbook or Book), when its directory lies inside the window.
/// </summary>
internal static class XlsSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return CompoundFile.Kind(head)?.Equals(FileFormat.Xls) == true;
    }
}
