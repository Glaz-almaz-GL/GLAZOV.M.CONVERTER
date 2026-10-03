namespace GLAZOV.M.CONVERTER;

/// <summary>
/// Состояние внешней программы: найдена ли, где лежит, что поставить, если нет.
/// </summary>
public sealed class ToolStatus
{
    internal ToolStatus(string name, bool found, string? path, string installHint)
    {
        Name = name;
        Found = found;
        Path = path;
        InstallHint = installHint;
    }

    /// <summary>
    /// Имя программы.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Найдена ли программа.
    /// </summary>
    public bool Found { get; }

    /// <summary>
    /// Путь к программе, если найдена.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Подсказка, что установить, для текущей системы.
    /// </summary>
    public string InstallHint { get; }
}
