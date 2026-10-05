using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Среда поиска внешних программ: переменная PATH, папка пакетов менеджера установки и система, чьи места поиска берутся из описаний.
/// Вынесено отдельно, чтобы поиск проверялся на подставных папках, а не только на настоящей системе.
/// </summary>
/// <param name="PathVariable">Значение переменной PATH.</param>
/// <param name="PackageFolder">Папка пакетов менеджера установки (WinGet на Windows), где искать подпапки с именем программы; пусто — нет такой.</param>
internal sealed record ToolEnvironment(string? PathVariable, string? PackageFolder)
{
    /// <summary>
    /// Система, по которой выбираются места поиска и подсказки установки из описаний программ.
    /// </summary>
    internal ToolSystem System { get; init; } = CurrentSystem();

    /// <summary>
    /// Turns a place of a <see cref="ToolSpec"/> into existing-or-not folders: environment variables are expanded and a wildcard in a folder name
    /// (<c>/opt/libreoffice*/program</c>) is replaced by every existing match, in name order. A missing or unreadable parent gives nothing.
    /// </summary>
    /// <param name="place">Folder pattern.</param>
    internal static IEnumerable<string> ExpandPlace(string place)
    {
        string expanded = Environment.ExpandEnvironmentVariables(place);
        int star = expanded.IndexOf('*');
        if (star < 0)
        {
            return [expanded];
        }

        int before = expanded.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], star);
        int after = expanded.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], star);
        string parent = before <= 0 ? Path.DirectorySeparatorChar.ToString() : expanded[..before];
        string pattern = expanded[(before + 1)..(after < 0 ? expanded.Length : after)];
        string rest = after < 0 ? string.Empty : expanded[after..];
        if (!Directory.Exists(parent))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(parent, pattern)
                .Order(StringComparer.Ordinal)
                .SelectMany(folder => ExpandPlace(folder + rest))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Среда настоящей системы: Windows, Linux или macOS.
    /// </summary>
    internal static ToolEnvironment ForThisSystem()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        string? packages = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages")
            : null;
        return new ToolEnvironment(path, packages);
    }

    private static ToolSystem CurrentSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            return ToolSystem.Windows;
        }

        return OperatingSystem.IsMacOS() ? ToolSystem.MacOS : ToolSystem.Linux;
    }
}
