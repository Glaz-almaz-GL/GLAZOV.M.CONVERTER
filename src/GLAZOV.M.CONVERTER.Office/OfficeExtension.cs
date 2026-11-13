using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Office.Engines;

namespace GLAZOV.M.CONVERTER.Office;

/// <summary>
/// Extension "Office": documents, spreadsheets, presentations and text to PDF, and tables (CSV, TSV, XLSX, XLS, ODS) into each other, through the external LibreOffice program.
/// Plugged in through <c>ConverterSettings.Extensions</c>; LibreOffice itself must be installed.
/// </summary>
public sealed class OfficeExtension : IConverterExtension
{
    /// <summary>
    /// Creates the extension with the default settings.
    /// </summary>
    public OfficeExtension()
        : this(new OfficeSettings())
    {
    }

    /// <summary>
    /// Creates the extension with the given settings. A copy is taken: later changes to <paramref name="settings"/> have no effect.
    /// </summary>
    /// <param name="settings">Outside resources, CSV separator, quote and encodings, the sheet of a book written as one file.</param>
    public OfficeExtension(OfficeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        OfficeSettings own = settings.Copy();
        Engines = [new LibreOfficeEngine(own), new TableEngine(own)];
    }

    /// <inheritdoc />
    public string Name => "Office";

    /// <inheritdoc />
    public IReadOnlyList<FileFormat> Formats => [.. Engines.SelectMany(engine => engine.Pairs).SelectMany(pair => new[] { pair.From, pair.To }).Distinct()];

    /// <inheritdoc />
    /// <remarks>
    /// Documents, delimited-text tables and, last, plain text: the detectors of the standard formats from the Formats package.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [StandardDetectors.Documents, StandardDetectors.Tables, StandardDetectors.Text];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; }
}
