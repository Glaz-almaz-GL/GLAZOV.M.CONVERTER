namespace GLAZOV.M.CONVERTER.Pdf.Rendering;

/// <summary>
/// Immutable copy of the PDF settings taken when the extension is created.
/// </summary>
/// <param name="MaxPixels">The largest picture in pixels.</param>
/// <param name="MaxInputBytes">The largest input in bytes.</param>
internal sealed record PdfLimits(long MaxPixels, long MaxInputBytes)
{
    /// <summary>
    /// Copies the settings.
    /// </summary>
    /// <param name="settings">Settings of the PDF engine.</param>
    internal static PdfLimits From(PdfSettings settings)
    {
        return new PdfLimits(settings.MaxPixels, settings.MaxInputBytes);
    }
}
