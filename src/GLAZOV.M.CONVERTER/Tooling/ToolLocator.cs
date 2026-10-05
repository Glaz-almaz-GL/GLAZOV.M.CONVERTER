using System.Collections.Concurrent;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Поиск внешней программы по её описанию (<see cref="ToolSpec"/>): путь из настроек, затем PATH, затем привычные места системы,
/// папки пакетов менеджера установки и папка другой программы. Путь из настроек, по которому файла нет, не откатывается молча на PATH:
/// ответ — «не найдена» с причиной. Результат кэшируется на время жизни локатора (то есть конвертера); ядро ничего не знает о самих программах.
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
    /// Ищет программу по описанию.
    /// </summary>
    /// <param name="spec">Описание программы.</param>
    /// <param name="siblings">Описания всех программ того же движка: среди них ищется программа, рядом с которой лежит эта (<see cref="ToolSpec.FoundBeside"/>).</param>
    internal ToolStatus Locate(ToolSpec spec, IReadOnlyList<ToolSpec>? siblings = null)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return _cache.GetOrAdd(spec.Name, _ => Find(spec, siblings ?? []));
    }

    private ToolStatus Find(ToolSpec spec, IReadOnlyList<ToolSpec> siblings)
    {
        if (_settings.ToolPaths.TryGetValue(spec.Name, out string? configured))
        {
            return File.Exists(configured)
                ? new ToolStatus(spec.Name, true, configured, string.Empty)
                : new ToolStatus(spec.Name, false, null, $"Путь из настроек (ToolPaths) не существует: {configured}. Исправьте путь или уберите запись, тогда поиск пойдёт по PATH.");
        }

        string? found = FindInPath(spec) ?? FindInPlaces(spec) ?? FindInPackages(spec) ?? FindBeside(spec, siblings);
        return found is not null
            ? new ToolStatus(spec.Name, true, found, string.Empty)
            : new ToolStatus(spec.Name, false, null, InstallHint(spec));
    }

    // A program may ship in the folder of another one (ffprobe with ffmpeg), whichever way that one was found (settings, PATH, package folder).
    private string? FindBeside(ToolSpec spec, IReadOnlyList<ToolSpec> siblings)
    {
        ToolSpec? other = spec.FoundBeside is null ? null : siblings.FirstOrDefault(s => string.Equals(s.Name, spec.FoundBeside, StringComparison.OrdinalIgnoreCase));
        if (other is null)
        {
            return null;
        }

        ToolStatus status = Locate(other, siblings);
        string? folder = status.Found ? Path.GetDirectoryName(status.Path) : null;
        return folder is null ? null : Candidate(folder, spec.Name);
    }

    private string? FindInPath(ToolSpec spec)
    {
        if (string.IsNullOrWhiteSpace(_environment.PathVariable))
        {
            return null;
        }

        foreach (string folder in _environment.PathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? candidate = CandidateByAnyName(folder, spec);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private string? FindInPlaces(ToolSpec spec)
    {
        if (!spec.SearchPlaces.TryGetValue(_environment.System, out IReadOnlyList<string>? places))
        {
            return null;
        }

        foreach (string folder in places.SelectMany(ToolEnvironment.ExpandPlace))
        {
            string? candidate = CandidateByAnyName(folder, spec);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    // Пакеты менеджера установки: подпапки с именем программы (например, Gyan.FFmpeg_...), внутри — bin\ffmpeg.exe.
    // Нужно, потому что установка не обновляет PATH уже запущенных процессов.
    private string? FindInPackages(ToolSpec spec)
    {
        string? root = _environment.PackageFolder;
        if (root is null || !Directory.Exists(root))
        {
            return null;
        }

        try
        {
            // A package folder may carry the name of a suite, not of the program (TheDocumentFoundation.LibreOffice holds soffice).
            string word = spec.PackageWord ?? spec.Name;
            foreach (string package in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            {
                if (!Path.GetFileName(package).Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? file = Directory.EnumerateFiles(package, ExecutableName(spec.Name), SearchOption.AllDirectories).FirstOrDefault();
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

    // The program under its own name first, then under the other names it goes by.
    private static string? CandidateByAnyName(string folder, ToolSpec spec)
    {
        return Candidate(folder, spec.Name) ?? spec.Aliases.Select(alias => Candidate(folder, alias)).FirstOrDefault(path => path is not null);
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

    private string InstallHint(ToolSpec spec)
    {
        return spec.InstallHints.TryGetValue(_environment.System, out string? hint)
            ? hint
            : $"Установите программу «{spec.Name}» и добавьте её в PATH или укажите путь в настройках (ToolPaths).";
    }
}
