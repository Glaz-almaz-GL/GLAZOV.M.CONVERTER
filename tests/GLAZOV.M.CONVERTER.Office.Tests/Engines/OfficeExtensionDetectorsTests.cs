using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// The extension brings the detectors of the formats its engine reads: documents, then plain text last.
/// </summary>
public sealed class OfficeExtensionDetectorsTests
{
    [Fact]
    public void Extension_NamesTheDocumentAndTableDetectors_AndTextLast()
    {
        Assert.Equal([StandardDetectors.Documents, StandardDetectors.Tables, StandardDetectors.Text], new OfficeExtension().Detectors);
    }
}
