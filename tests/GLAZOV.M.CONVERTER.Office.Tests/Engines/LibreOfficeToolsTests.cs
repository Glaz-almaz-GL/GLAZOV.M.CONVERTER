using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Office.Engines;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Office.Tests.Engines;

/// <summary>
/// The description of the LibreOffice program: the usual places on every system, its other name, the install hints and a real find.
/// </summary>
public sealed class LibreOfficeToolsTests
{
    [Fact]
    public void Description_KnowsTheUsualPlacesAndTheOtherNameOfTheProgram()
    {
        Assert.Contains(LibreOfficeTools.Soffice.SearchPlaces[ToolSystem.Windows], place => place.EndsWith(Path.Combine("LibreOffice", "program"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains("/Applications/LibreOffice.app/Contents/MacOS", LibreOfficeTools.Soffice.SearchPlaces[ToolSystem.MacOS]);
        Assert.Contains("/opt/libreoffice*/program", LibreOfficeTools.Soffice.SearchPlaces[ToolSystem.Linux]);
        Assert.Contains("libreoffice", LibreOfficeTools.Soffice.Aliases);
        Assert.Equal("LibreOffice", LibreOfficeTools.Soffice.PackageWord);
    }

    [Fact]
    public void Description_GivesAnInstallHintForEverySystem_NamingTheToolPathsSetting()
    {
        Assert.Contains("winget install TheDocumentFoundation.LibreOffice", LibreOfficeTools.Soffice.InstallHints[ToolSystem.Windows]);
        Assert.Contains("brew install --cask libreoffice", LibreOfficeTools.Soffice.InstallHints[ToolSystem.MacOS]);
        Assert.Contains("apt install libreoffice-core libreoffice-writer libreoffice-calc libreoffice-impress", LibreOfficeTools.Soffice.InstallHints[ToolSystem.Linux]);
        Assert.All(LibreOfficeTools.Soffice.InstallHints.Values, hint => Assert.Contains("ToolPaths", hint));
    }

    // Only found, never started: soffice.exe --version opens a console window that waits for Enter.
    [OfficeFact]
    public void RealSoffice_IsFound()
    {
        ToolLocator locator = new(ConverterSettingsSnapshot.Create(new ConverterSettings()));

        ToolStatus status = locator.Locate(LibreOfficeTools.Soffice);

        Assert.True(status.Found);
        Assert.True(File.Exists(status.Path));
    }
}
