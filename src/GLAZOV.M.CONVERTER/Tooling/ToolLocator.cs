using System.Collections.Concurrent;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Поиск внешней программы: путь из настроек, затем PATH, затем привычные места системы.
/// Путь из настроек, по которому файла нет, не откатывается молча на PATH: ответ — «не найдена» с причиной.
/// Результат кэшируется на время жизни локатора (то есть конвертера).
/// </summary>
internal sealed class ToolLocator
{
    private readonly ConverterSettingsSnapshot _settings;
    private readonly ToolEnvironment _environment;
    private readonly ConcurrentDictionary<string, ToolStatus> _cache = new(StringComparer.OrdinalIgnoreCase);

    internal ToolLocator(ConverterSettingsSnapshot settings, ToolEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _environment = environment ?? ToolEnvironment.ForThisSystem();
    }

    /// <summary>
    /// Ищет программу по имени (без расширения: <c>ffmpeg</c>).
    /// </summary>
    /// <param name="name">Имя программы.</param>
    internal ToolStatus Locate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _cache.GetOrAdd(name, Find);
    }

    private ToolStatus Find(string name)
    {
        if (_settings.ToolPaths.TryGetValue(name, out string? configured))
        {
            return File.Exists(configured)
                ? new ToolStatus(name, true, configured, string.Empty)
                : new ToolStatus(name, false, null, $"Путь из настроек (ToolPaths) не существует: {configured}. Исправьте путь или уберите запись, тогда поиск пойдёт по PATH.");
        }

        string? found = FindInPath(name) ?? FindInUsualFolders(name) ?? FindInPackages(name);
        return found is not null
            ? new ToolStatus(name, true, found, string.Empty)
            : new ToolStatus(name, false, null, InstallHint(name));
    }

    private string? FindInPath(string name)
    {
        if (string.IsNullOrWhiteSpace(_environment.PathVariable))
        {
            return null;
        }

        foreach (string folder in _environment.PathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? candidate = Candidate(folder, name);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private string? FindInUsualFolders(string name)
    {
        foreach (string folder in _environment.UsualFolders)
        {
            string? candidate = Candidate(folder, name);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    // Пакеты менеджера установки: подпапки с именем программы (например, Gyan.FFmpeg_...), внутри — bin\ffmpeg.exe.
    // Нужно, потому что установка не обновляет PATH уже запущенных процессов.
    private string? FindInPackages(string name)
    {
        string? root = _environment.PackageFolder;
        if (root is null || !Directory.Exists(root))
        {
            return null;
        }

        try
        {
            foreach (string package in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            {
                if (!Path.GetFileName(package).Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? file = Directory.EnumerateFiles(package, ExecutableName(name), SearchOption.AllDirectories).FirstOrDefault();
                if (file is not null)
                {
                    return file;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Папка пакетов недоступна — это «не нашли здесь», а не ошибка поиска.
            return null;
        }

        return null;
    }

    private static string? Candidate(string folder, string name)
    {
        try
        {
            string path = Path.Combine(folder, ExecutableName(name));
            return File.Exists(path) && IsExecutable(path) ? path : null;
        }
        catch (ArgumentException)
        {
            // Некорректная запись в PATH пропускается.
            return null;
        }
    }

    private static string ExecutableName(string name)
    {
        return OperatingSystem.IsWindows() ? name + ".exe" : name;
    }

    private static bool IsExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return (File.GetUnixFileMode(path) & anyExecute) != 0;
    }

    private static string InstallHint(string name)
    {
        if (!string.Equals(name, "ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            return $"Установите программу «{name}» и добавьте её в PATH или укажите путь в настройках (ToolPaths).";
        }

        if (OperatingSystem.IsWindows())
        {
            return "Установите: winget install Gyan.FFmpeg (затем перезапустите программу) либо укажите путь в настройках (ToolPaths).";
        }

        return OperatingSystem.IsMacOS()
            ? "Установите: brew install ffmpeg либо укажите путь в настройках (ToolPaths)."
            : "Установите: sudo apt install ffmpeg (или пакет ffmpeg вашего дистрибутива) либо укажите путь в настройках (ToolPaths).";
    }
}
