using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;

namespace GLAZOV.M.CONVERTER.Errors;

/// <summary>
/// Единая фабрика ошибок конвертации: верные этап и причина и понятное сообщение по-русски.
/// В сообщениях только имя файла, без полных путей.
/// </summary>
internal static class Failures
{
    /// <summary>
    /// Сколько последних строк вывода внешней программы сохраняется в ошибке.
    /// </summary>
    internal const int ToolOutputLines = 20;

    /// <summary>
    /// Сколько символов вывода внешней программы сохраняется в ошибке.
    /// </summary>
    internal const int ToolOutputChars = 2000;

    /// <summary>
    /// Тип входа не подтвердился по начальным байтам.
    /// </summary>
    /// <param name="inputName">Имя входа.</param>
    /// <param name="expectedHint">Чем вход назван (например, MP3 по расширению); пусто — ничем.</param>
    /// <param name="actual">Что показали начальные байты.</param>
    internal static ConversionException TypeNotConfirmed(string inputName, string? expectedHint, FileFormat actual)
    {
        var found = ReferenceEquals(actual, FileFormat.Unknown)
            ? "тип не распознан"
            : $"это {actual.Name}";
        var message = string.IsNullOrWhiteSpace(expectedHint)
            ? $"Тип входа «{inputName}» не подтвердился: по начальным байтам {found}."
            : $"Файл «{inputName}» не является {expectedHint}: по начальным байтам {found}.";

        return new ConversionException(ConversionStage.Detection, ConversionFailureReason.TypeNotConfirmed, message);
    }

    /// <summary>
    /// Такая конвертация не поддерживается.
    /// </summary>
    /// <param name="from">Тип входа.</param>
    /// <param name="to">Формат результата.</param>
    internal static ConversionException UnsupportedConversion(FileFormat from, FileFormat to) =>
        new(
            ConversionStage.Preparation,
            ConversionFailureReason.UnsupportedConversion,
            $"Конвертация {from.Name} → {to.Name} не поддерживается.");

    /// <summary>
    /// Нужная внешняя программа не найдена.
    /// </summary>
    /// <param name="tool">Имя программы.</param>
    /// <param name="installHint">Что установить на этой системе.</param>
    internal static ConversionException ToolNotFound(string tool, string installHint) =>
        new(
            ConversionStage.Preparation,
            ConversionFailureReason.ToolNotFound,
            $"Программа «{tool}» не найдена. {installHint}",
            toolName: tool);

    /// <summary>
    /// Внешняя программа завершилась с ошибкой.
    /// </summary>
    /// <param name="tool">Имя программы.</param>
    /// <param name="exitCode">Код выхода.</param>
    /// <param name="output">Вывод программы; сохраняются последние строки.</param>
    internal static ConversionException ToolFailed(string tool, int exitCode, string? output)
    {
        var tail = TrimOutput(output);
        var last = tail.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        var message = string.IsNullOrEmpty(last)
            ? $"Программа «{tool}» завершилась с ошибкой (код {exitCode})."
            : $"Программа «{tool}» завершилась с ошибкой (код {exitCode}): {last}";

        return new ConversionException(
            ConversionStage.Conversion,
            ConversionFailureReason.ToolFailed,
            message,
            toolName: tool,
            toolExitCode: exitCode,
            toolOutput: tail);
    }

    /// <summary>
    /// Файл результата уже существует, а перезапись запрещена.
    /// </summary>
    /// <param name="path">Путь к файлу; в сообщение попадает только имя.</param>
    internal static ConversionException FileExists(string path) =>
        new(
            ConversionStage.Preparation,
            ConversionFailureReason.FileExists,
            $"Файл «{Path.GetFileName(path)}» уже существует; разрешите перезапись настройкой OverwriteExisting.");

    /// <summary>
    /// Вход не удалось прочитать из-за ошибки доступа.
    /// </summary>
    /// <param name="inputName">Имя входа.</param>
    /// <param name="inner">Исходное исключение.</param>
    internal static ConversionException InputUnreadable(string inputName, Exception inner) =>
        new(
            ConversionStage.Preparation,
            ConversionFailureReason.InputUnreadable,
            $"Не удалось прочитать вход «{inputName}»: {inner.Message}",
            innerException: inner);

    /// <summary>
    /// Вход не читается по названной причине (не из исключения).
    /// </summary>
    /// <param name="inputName">Имя входа.</param>
    /// <param name="detail">Что не так.</param>
    internal static ConversionException InputUnreadable(string inputName, string detail) =>
        new(
            ConversionStage.Preparation,
            ConversionFailureReason.InputUnreadable,
            $"Вход «{inputName}» не читается: {detail}");

    /// <summary>
    /// Оставляет последние <see cref="ToolOutputLines"/> строк вывода, не больше <see cref="ToolOutputChars"/> символов.
    /// </summary>
    /// <param name="output">Вывод программы.</param>
    internal static string TrimOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var tail = string.Join('\n', lines.TakeLast(ToolOutputLines)).TrimEnd();
        return tail.Length <= ToolOutputChars ? tail : tail[^ToolOutputChars..];
    }
}
