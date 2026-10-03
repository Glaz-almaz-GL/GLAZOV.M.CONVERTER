namespace GLAZOV.M.CONVERTER.Settings;

/// <summary>
/// Неизменяемая проверенная копия <see cref="ConverterSettings"/> на момент создания конвертера:
/// последующие правки исходного объекта на поведение не влияют.
/// </summary>
internal sealed class ConverterSettingsSnapshot
{
    private ConverterSettingsSnapshot(
        int maxParallelTasks,
        string? tempFolder,
        IReadOnlyDictionary<string, string> toolPaths,
        bool overwriteExisting)
    {
        MaxParallelTasks = maxParallelTasks;
        TempFolder = tempFolder;
        ToolPaths = toolPaths;
        OverwriteExisting = overwriteExisting;
    }

    /// <summary>
    /// Сколько задач выполняется одновременно, не меньше 1.
    /// </summary>
    internal int MaxParallelTasks { get; }

    /// <summary>
    /// Существующая папка временных файлов; пусто — системная временная папка.
    /// </summary>
    internal string? TempFolder { get; }

    /// <summary>
    /// Пути к внешним программам: имя (без учёта регистра) → непустой путь.
    /// </summary>
    internal IReadOnlyDictionary<string, string> ToolPaths { get; }

    /// <summary>
    /// Перезаписывать ли существующие файлы.
    /// </summary>
    internal bool OverwriteExisting { get; }

    /// <summary>
    /// Копирует и проверяет настройки; неверное значение — <see cref="ArgumentException"/> с понятным сообщением.
    /// </summary>
    /// <param name="settings">Настройки, заданные разработчиком.</param>
    internal static ConverterSettingsSnapshot Create(ConverterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.MaxParallelTasks < 1)
        {
            throw new ArgumentException(
                $"Параметр MaxParallelTasks должен быть не меньше 1, получено {settings.MaxParallelTasks}.",
                nameof(settings));
        }

        string? tempFolder = string.IsNullOrWhiteSpace(settings.TempFolder) ? null : settings.TempFolder;
        if (tempFolder is not null && !Directory.Exists(tempFolder))
        {
            throw new ArgumentException(
                $"Папка временных файлов «{tempFolder}» (параметр TempFolder) не существует.",
                nameof(settings));
        }

        Dictionary<string, string> toolPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string path) in settings.ToolPaths)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    $"Путь к программе «{name}» (параметр ToolPaths) пуст: укажите путь или уберите запись.",
                    nameof(settings));
            }

            toolPaths[name] = path;
        }

        return new ConverterSettingsSnapshot(settings.MaxParallelTasks, tempFolder, toolPaths, settings.OverwriteExisting);
    }
}
