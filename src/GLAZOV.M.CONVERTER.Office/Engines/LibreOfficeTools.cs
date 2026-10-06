using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Office.Engines;

/// <summary>
/// Description of the LibreOffice program the document engine needs.
/// </summary>
internal static class LibreOfficeTools
{
    /// <summary>
    /// <c>soffice</c>; on Linux the program often goes by <c>libreoffice</c>, and a WinGet package folder carries the name of the suite.
    /// </summary>
    internal static ToolSpec Soffice { get; } = Create();

    private static ToolSpec Create()
    {
        ToolSpec spec = new("soffice") { Aliases = ["libreoffice"], PackageWord = "LibreOffice" };
        spec.SearchPlaces[ToolSystem.Windows] = [@"%ProgramFiles%\LibreOffice\program", @"%ProgramFiles(x86)%\LibreOffice\program"];
        spec.SearchPlaces[ToolSystem.MacOS] = ["/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin", "/Applications/LibreOffice.app/Contents/MacOS"];
        spec.SearchPlaces[ToolSystem.Linux] = ["/usr/bin", "/usr/local/bin", "/snap/bin", "/opt/libreoffice*/program"];
        spec.InstallHints[ToolSystem.Windows] = "Установите: winget install TheDocumentFoundation.LibreOffice либо укажите путь к soffice.exe в настройках (ToolPaths).";
        spec.InstallHints[ToolSystem.MacOS] = "Установите: brew install --cask libreoffice либо укажите путь к soffice в настройках (ToolPaths).";
        spec.InstallHints[ToolSystem.Linux] = "Установите: sudo apt install libreoffice-core libreoffice-writer libreoffice-calc libreoffice-impress (или пакет вашего дистрибутива) либо укажите путь к soffice в настройках (ToolPaths).";
        return spec;
    }
}
