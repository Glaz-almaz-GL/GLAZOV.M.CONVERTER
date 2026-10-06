using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Settings;

/// <summary>
/// Настройки библиотеки. Передаются в конструктор <see cref="Converter"/> и после этого не меняют его поведения;
/// общих настроек «на всё приложение» нет.
/// </summary>
public sealed class ConverterSettings
{
    /// <summary>
    /// Окно определения формата по умолчанию: 64 КБ.
    /// </summary>
    internal const int DefaultDetectionWindow = 64 * 1024;

    /// <summary>
    /// Сколько задач выполняется одновременно. По умолчанию — число ядер; жёсткого потолка нет.
    /// </summary>
    public int MaxParallelTasks { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Папка временных файлов. Пусто — системная временная папка.
    /// </summary>
    public string? TempFolder { get; set; }

    /// <summary>
    /// Где искать внешние программы: имя программы → путь. Пусто — поиск в PATH и привычных местах.
    /// </summary>
    public IDictionary<string, string> ToolPaths { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Перезаписывать ли существующие файлы. По умолчанию нет: будет ошибка «файл существует».
    /// </summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>
    /// Сколько начальных байтов входа читается для определения формата. По умолчанию 64 КБ, не меньше 4096.
    /// Больше всего окна нужно SVG с длинным вступлением (комментарии, DOCTYPE) до корневого <c>svg</c>:
    /// вступление длиннее окна даёт «тип не распознан». Начало входа держится в памяти, пока идёт определение.
    /// </summary>
    public int DetectionWindow { get; set; } = DefaultDetectionWindow;

    /// <summary>
    /// Подключаемые расширения (сторонние форматы и движки). Подключаются при создании конвертера и дальше не меняются.
    /// Расширение выполняется в вашем процессе: подключайте только то, чему доверяете.
    /// </summary>
    public ICollection<IConverterExtension> Extensions { get; } = [];
}
