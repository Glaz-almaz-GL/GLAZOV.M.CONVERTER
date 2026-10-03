namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Что запустить: программа, аргументы списком (без экранирования), каналы стандартного ввода и вывода.
/// </summary>
/// <param name="Path">Путь к программе.</param>
/// <param name="Arguments">Аргументы списком: каждый передаётся как есть, пробелы и кавычки не требуют экранирования.</param>
/// <param name="StandardInput">Поток, содержимое которого подаётся программе на стандартный ввод; после копирования ввод закрывается.</param>
/// <param name="StandardOutput">Поток, в который копируется стандартный вывод программы.</param>
/// <param name="WorkingDirectory">Рабочая папка; пусто — текущая.</param>
/// <param name="OnStarted">Вызывается с номером процесса сразу после запуска (для диагностики и проверок).</param>
internal sealed record ToolInvocation(
    string Path,
    IReadOnlyList<string> Arguments,
    Stream? StandardInput = null,
    Stream? StandardOutput = null,
    string? WorkingDirectory = null,
    Action<int>? OnStarted = null);
