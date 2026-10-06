using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Documents;

/// <summary>
/// ODP: OpenDocument presentation, told by what the archive holds inside the window.
/// </summary>
internal static class OdpSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return ZipPackage.Kind(head)?.Equals(FileFormat.Odp) == true;
    }
}
