namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Где искать внешние программы кроме явно заданного пути: переменная PATH и привычные места системы.
/// Вынесено отдельно, чтобы поиск проверялся на подставных папках, а не только на настоящей системе.
/// </summary>
/// <param name="PathVariable">Значение переменной PATH.</param>
/// <param name="UsualFolders">Привычные папки установки программ на этой системе, по порядку поиска.</param>
/// <param name="PackageFolder">Папка пакетов менеджера установки (WinGet на Windows), где искать подпапки с именем программы; пусто — нет такой.</param>
internal sealed record ToolEnvironment(string? PathVariable, IReadOnlyList<string> UsualFolders, string? PackageFolder)
{
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
                @"C:\ffmpeg\bin",
            ];
            return new ToolEnvironment(path, folders, Path.Combine(local, "Microsoft", "WinGet", "Packages"));
        }

        if (OperatingSystem.IsMacOS())
        {
            return new ToolEnvironment(path, ["/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin"], null);
        }

        return new ToolEnvironment(path, ["/usr/bin", "/usr/local/bin", "/snap/bin", "/opt/ffmpeg/bin"], null);
    }
}
