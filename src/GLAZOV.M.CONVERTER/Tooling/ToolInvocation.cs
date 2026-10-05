namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Что запустить: программа, аргументы списком (без экранирования), каналы стандартного ввода и вывода.
/// </summary>
/// <param name="Path">Путь к программе.</param>
/// <param name="Arguments">Аргументы списком: каждый передаётся как есть, пробелы и кавычки не требуют экранирования.</param>
/// <param name="StandardInput">Поток, содержимое которого подаётся программе на стандартный ввод; после копирования ввод закрывается.</param>
/// <param name="StandardOutput">Поток, в который копируется стандартный вывод программы.</param>
/// <param name="WorkingDirectory">Рабочая папка; пусто — текущая.</param>
/// <param name="OnErrorLine">Called with each line of the standard error as it arrives, before the process ends.</param>
/// <param name="OnOutputLine">Called with each line of the standard output as it arrives; cannot be combined with <paramref name="StandardOutput"/>.</param>
/// <param name="OnStarted">Вызывается с номером процесса сразу после запуска (для диагностики и проверок).</param>
public sealed record ToolInvocation(
    string Path,
    IReadOnlyList<string> Arguments,
    Stream? StandardInput = null,
    Stream? StandardOutput = null,
    string? WorkingDirectory = null,
    Action<int>? OnStarted = null,
    Action<string>? OnErrorLine = null,
    Action<string>? OnOutputLine = null);
