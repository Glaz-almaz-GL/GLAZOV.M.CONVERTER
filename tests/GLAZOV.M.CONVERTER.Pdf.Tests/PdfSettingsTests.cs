namespace GLAZOV.M.CONVERTER.Pdf.Tests;

/// <summary>
/// Settings of the PDF engine: defaults and validation.
/// </summary>
public sealed class PdfSettingsTests
{
    [Fact]
    public void Defaults_AreFiftyMillionPixels_And256Megabytes()
    {
        PdfSettings settings = new();

        Assert.Equal(50_000_000, settings.MaxPixels);
        Assert.Equal(256L * 1024 * 1024, settings.MaxInputBytes);
    }

    [Fact]
    public void ZeroOrNegativeLimits_AreRejected()
    {
        PdfSettings settings = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxPixels = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxInputBytes = -1);
    }

    [Fact]
    public void ExtensionKeepsACopy_LaterChangesDoNotReachIt()
    {
        PdfSettings settings = new() { MaxPixels = 1_000_000 };
        PdfExtension extension = new(settings);

        settings.MaxPixels = 1;

        Assert.Equal("PDF", extension.Name);
        Assert.Single(extension.Engines);
    }
}
