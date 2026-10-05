namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Описание внешней программы, которая нужна движку: как её называть в настройках, где искать и что посоветовать,
/// если не найдена. Ядро ищет программу только по этим данным и ничего о ней не знает.
/// Порядок поиска: путь из <c>ConverterSettings.ToolPaths</c>, переменная PATH, <see cref="SearchPlaces"/>,
/// папки пакетов менеджера установки, папка другой программы (<see cref="FoundBeside"/>).
/// </summary>
public sealed class ToolSpec
{
    /// <summary>
    /// Создаёт описание программы.
    /// </summary>
    /// <param name="name">Имя программы без расширения, как в <c>ConverterSettings.ToolPaths</c>.</param>
    public ToolSpec(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// Имя программы без расширения; по нему находят её в настройках и в <c>ConversionRequest.Tools</c> (без учёта регистра).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Другие имена файла, под которыми программа встречается (например, <c>libreoffice</c> для <c>soffice</c>); пробуются после основного.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>
    /// <c>true</c> — без программы движок не работает (поиск без результата — отказ с подсказкой установки);
    /// <c>false</c> — программа только улучшает работу, её отсутствие не ошибка. По умолчанию <c>true</c>.
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Имя другой программы из <c>Tools</c> того же движка: если эта не найдена иначе, ищется рядом с той (<c>ffprobe</c> рядом с <c>ffmpeg</c>).
    /// </summary>
    public string? FoundBeside { get; init; }

    /// <summary>
    /// Слово в имени подпапки пакета менеджера установки (WinGet), где лежит программа; пусто — имя программы.
    /// </summary>
    public string? PackageWord { get; init; }

    /// <summary>
    /// Привычные места установки по системам, по порядку поиска. В пути допустимы переменные среды (<c>%ProgramFiles%</c>)
    /// и звёздочка в имени папки (<c>/opt/libreoffice*/program</c>); найденные по звёздочке папки идут в порядке имён.
    /// </summary>
    public IDictionary<ToolSystem, IReadOnlyList<string>> SearchPlaces { get; } = new Dictionary<ToolSystem, IReadOnlyList<string>>();

    /// <summary>
    /// Подсказка «что установить» по системам; нет подсказки для системы — общая фраза.
    /// </summary>
    public IDictionary<ToolSystem, string> InstallHints { get; } = new Dictionary<ToolSystem, string>();
}
