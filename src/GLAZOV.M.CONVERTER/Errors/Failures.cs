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
    /// Действия (поворот, масштаб) запрошены там, где они не поддерживаются. Для результата-звука сообщение называет звук.
    /// </summary>
    /// <param name="from">Формат входа.</param>
    /// <param name="to">Формат результата.</param>
    internal static ConversionException UnsupportedOperations(FileFormat from, FileFormat to)
    {
        FileFormat[] sound = [FileFormat.Mp3, FileFormat.Wav, FileFormat.Ogg, FileFormat.Flac, FileFormat.M4a];
        string message = sound.Contains(to)
            ? $"Поворот и масштаб не применимы к звуку: {from.Name} → {to.Name}."
            : $"Действия не поддерживаются для {from.Name} → {to.Name}.";

        return new ConversionException(ConversionStage.Preparation, ConversionFailureReason.UnsupportedConversion, message);
    }

    /// <summary>
    /// Два входа пакета дают результат с одним именем: обработан первый, остальные отвергаются, чтобы не затирать друг друга.
    /// </summary>
    /// <param name="inputName">Имя отвергнутого входа.</param>
    /// <param name="outputName">Общее имя результата.</param>
    internal static ConversionException DuplicateOutput(string inputName, string outputName)
    {
        return new ConversionException(
            ConversionStage.Preparation,
            ConversionFailureReason.FileExists,
            $"Вход «{inputName}» пропущен: результат «{outputName}» уже занят другим входом этого пакета.");
    }

    /// <summary>
    /// Движок расширения упал непредвиденно: ошибка конвертации с исходным исключением внутри.
    /// </summary>
    /// <param name="engineName">Имя типа движка.</param>
    /// <param name="inner">Исходное исключение.</param>
    internal static ConversionException EngineFailed(string engineName, Exception inner)
    {
        return new ConversionException(
            ConversionStage.Conversion,
            ConversionFailureReason.EngineFailed,
            $"Движок «{engineName}» завершился ошибкой: {inner.Message}",
            innerException: inner);
    }

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
            $"Не удалось прочитать вход «{inputName}»: {Describe(inner)}.",
            innerException: inner);

    // Сообщение системы содержит полный путь (его нельзя показывать, правило «только имя файла»): причина называется своими словами,
    // а само исключение остаётся во внутренней части ошибки.
    private static string Describe(Exception error)
    {
        return error switch
        {
            FileNotFoundException => "файл не найден",
            DirectoryNotFoundException => "папка не найдена",
            UnauthorizedAccessException => "нет доступа",
            PathTooLongException => "слишком длинный путь",
            IOException => "файл занят другой программой или недоступен",
            NotSupportedException or ArgumentException => "недопустимый путь",
            _ => "ошибка чтения",
        };
    }

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
    /// The result could not be written or created. The message names the result by its file name only.
    /// </summary>
    /// <param name="name">File name of the result, or «поток».</param>
    /// <param name="inner">The system error; kept as the inner exception.</param>
    /// <param name="stage">Preparation when the output could not be opened, conversion when writing broke off.</param>
    internal static ConversionException OutputUnwritable(string name, Exception inner, ConversionStage stage) =>
        new(
            stage,
            ConversionFailureReason.OutputUnwritable,
            $"Не удалось записать результат «{name}»: {DescribeOutput(inner)}.",
            innerException: inner);

    // The system message holds the full path, so the cause is named in plain words and the exception stays inside.
    private static string DescribeOutput(Exception error)
    {
        return error switch
        {
            UnauthorizedAccessException => "нет доступа",
            DirectoryNotFoundException => "папка не найдена",
            PathTooLongException => "слишком длинный путь",
            ArgumentException or NotSupportedException => "недопустимый путь",
            IOException io when IsDiskFull(io) => "нет места на диске",
            IOException => "запись не удалась или файл занят другой программой",
            _ => "запись не удалась",
        };
    }

    // ERROR_HANDLE_DISK_FULL (0x27) and ERROR_DISK_FULL (0x70) on Windows, ENOSPC (28) elsewhere.
    private static bool IsDiskFull(IOException error)
    {
        int code = error.HResult & 0xFFFF;
        return code is 0x27 or 0x70 or 28;
    }

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
