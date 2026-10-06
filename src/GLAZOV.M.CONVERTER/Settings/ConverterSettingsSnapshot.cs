using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Extensions;

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
        bool overwriteExisting,
        int detectionWindow,
        IReadOnlyList<IConverterExtension> extensions)
    {
        DetectionWindow = detectionWindow;
        MaxParallelTasks = maxParallelTasks;
        TempFolder = tempFolder;
        ToolPaths = toolPaths;
        OverwriteExisting = overwriteExisting;
        Extensions = extensions;
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
    /// Подключённые расширения по порядку; имена непусты и уникальны, списки определителей и движков не пусты в смысле «не null».
    /// </summary>
    internal IReadOnlyList<IConverterExtension> Extensions { get; }

    /// <summary>
    /// Перезаписывать ли существующие файлы.
    /// </summary>
    internal bool OverwriteExisting { get; }

    /// <summary>
    /// Окно определения формата в байтах, не меньше наименьшего окна готовых признаков.
    /// </summary>
    internal int DetectionWindow { get; }

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

        if (settings.DetectionWindow < DetectionSizes.MinimumWindow)
        {
            throw new ArgumentException(
                $"Параметр DetectionWindow должен быть не меньше {DetectionSizes.MinimumWindow} байт, получено {settings.DetectionWindow}.",
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

        return new ConverterSettingsSnapshot(settings.MaxParallelTasks, tempFolder, toolPaths, settings.OverwriteExisting, settings.DetectionWindow, CheckExtensions(settings.Extensions));
    }

    // Расширения проверяются при создании: чужой код с пустым именем, повтором имени или пустыми списками отвергается сразу.
    private static IReadOnlyList<IConverterExtension> CheckExtensions(IEnumerable<IConverterExtension> extensions)
    {
        List<IConverterExtension> list = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (IConverterExtension extension in extensions)
        {
            if (extension is null)
            {
                throw new ArgumentException("В списке расширений (параметр Extensions) есть пустая запись.", "settings");
            }

            string name = extension.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"У расширения {extension.GetType().Name} пустое имя (Name): имя обязательно.", "settings");
            }

            if (!names.Add(name))
            {
                throw new ArgumentException($"Расширение «{name}» подключено дважды: имена расширений уникальны.", "settings");
            }

            if (extension.Detectors is null || extension.Engines is null || extension.Detectors.Any(d => d is null) || extension.Engines.Any(e => e is null))
            {
                throw new ArgumentException($"У расширения «{name}» списки определителей и движков не заданы или содержат пустые записи.", "settings");
            }

            if (extension.Detectors.Any(d => d.HeadLength < 1))
            {
                throw new ArgumentException($"У расширения «{name}» определитель просит меньше одного байта (HeadLength).", "settings");
            }

            list.Add(extension);
        }

        return list;
    }
}
