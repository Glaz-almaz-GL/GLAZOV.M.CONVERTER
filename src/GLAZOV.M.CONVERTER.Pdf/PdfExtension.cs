using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Pdf.Engines;
using GLAZOV.M.CONVERTER.Pdf.Rendering;

namespace GLAZOV.M.CONVERTER.Pdf;

/// <summary>
/// Extension "PDF": turns a PNG, JPEG, WebP, BMP or GIF picture into a PDF of one page
/// as large as the picture (one pixel is one point). Plugged in with <c>ConverterSettings.Extensions</c>.
/// Needs no external program.
/// </summary>
public sealed class PdfExtension : IConverterExtension
{
    /// <summary>
    /// Creates the extension with default settings.
    /// </summary>
    public PdfExtension()
        : this(new PdfSettings())
    {
    }

    /// <summary>
    /// Creates the extension with the given settings; their values are copied.
    /// </summary>
    /// <param name="settings">Settings of the PDF engine.</param>
    public PdfExtension(PdfSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Engines = [new ImagePdfEngine(PdfLimits.From(settings))];
    }

    /// <inheritdoc />
    public string Name => "PDF";

    /// <inheritdoc />
    /// <remarks>
    /// Empty: the input types are recognised by the main library.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; }
}
