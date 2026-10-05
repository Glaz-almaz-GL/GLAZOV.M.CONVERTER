namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Где искать внешние программы кроме явно заданного пути: переменная PATH и привычные места системы.
/// Вынесено отдельно, чтобы поиск проверялся на подставных папках, а не только на настоящей системе.
/// </summary>
/// <param name="PathVariable">Значение переменной PATH.</param>
/// <param name="UsualFolders">Привычные папки установки программ на этой системе, по порядку поиска.</param>
/// <param name="PackageFolder">Папка пакетов менеджера установки (WinGet на Windows), где искать подпапки с именем программы; пусто — нет такой.</param>
/// <param name="Aliases">Other file names a program goes by on this system, by its name (<c>soffice</c> is <c>libreoffice</c> on Linux); <c>null</c> — none.</param>
internal sealed record ToolEnvironment(
    string? PathVariable,
    IReadOnlyList<string> UsualFolders,
    string? PackageFolder,
    IReadOnlyDictionary<string, string[]>? Aliases = null)
{
    /// <summary>
    /// Expands a folder pattern whose last segment may end with <c>*</c> (<c>/opt/libreoffice*</c>) into the existing folders, in name order.
    /// </summary>
    /// <param name="parent">Folder holding the matches.</param>
    /// <param name="prefix">Start of the folder name.</param>
    /// <param name="inside">Subfolder of each match that holds the programs.</param>
    internal static IEnumerable<string> ExpandFolders(string parent, string prefix, string inside)
    {
        if (!Directory.Exists(parent))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(parent, prefix + "*")
                .Order(StringComparer.Ordinal)
                .Select(folder => Path.Combine(folder, inside))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Окружение настоящей системы: Windows, Linux или macOS.
    /// </summary>
    internal static ToolEnvironment ForThisSystem()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");

        if (OperatingSystem.IsWindows())
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] folders =
            [
                Path.Combine(programFiles, "ffmpeg", "bin"),
                Path.Combine(programFilesX86, "ffmpeg", "bin"),
                Path.Combine(local, "Microsoft", "WinGet", "Links"),
                Path.Combine(local, "Programs", "ffmpeg", "bin"),
                Path.Combine(programFiles, "LibreOffice", "program"),
                Path.Combine(programFilesX86, "LibreOffice", "program"),
                @"C:\ffmpeg\bin",
            ];
            return new ToolEnvironment(path, folders, Path.Combine(local, "Microsoft", "WinGet", "Packages"));
        }

        if (OperatingSystem.IsMacOS())
        {
            return new ToolEnvironment(path, ["/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin", "/Applications/LibreOffice.app/Contents/MacOS"], null);
        }

        string[] linux = ["/usr/bin", "/usr/local/bin", "/snap/bin", "/opt/ffmpeg/bin", .. ExpandFolders("/opt", "libreoffice", "program")];
        return new ToolEnvironment(path, linux, null, new Dictionary<string, string[]> { ["soffice"] = ["libreoffice"] });
    }
}
