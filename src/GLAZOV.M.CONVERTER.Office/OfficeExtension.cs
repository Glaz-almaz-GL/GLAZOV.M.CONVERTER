using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Office.Engines;

namespace GLAZOV.M.CONVERTER.Office;

/// <summary>
/// Extension "Office": documents, spreadsheets, presentations and text to PDF through the external LibreOffice program.
/// Plugged in through <c>ConverterSettings.Extensions</c>; LibreOffice itself must be installed.
/// </summary>
public sealed class OfficeExtension : IConverterExtension
{
    /// <inheritdoc />
    public string Name => "Office";

    /// <inheritdoc />
    public IReadOnlyList<FileFormat> Formats => [.. Engines.SelectMany(engine => engine.Pairs).SelectMany(pair => new[] { pair.From, pair.To }).Distinct()];

    /// <inheritdoc />
    /// <remarks>
    /// Empty: the formats are recognised by the detectors of the core library.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; } = [new LibreOfficeEngine()];
}
