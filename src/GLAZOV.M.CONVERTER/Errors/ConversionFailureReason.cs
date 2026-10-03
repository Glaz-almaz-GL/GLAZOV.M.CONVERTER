namespace GLAZOV.M.CONVERTER.Errors;

/// <summary>
/// Причина ошибки конвертации.
/// </summary>
public enum ConversionFailureReason
{
    /// <summary>
    /// Тип входа не подтвердился по начальным байтам.
    /// </summary>
    TypeNotConfirmed,

    /// <summary>
    /// Такая конвертация не поддерживается.
    /// </summary>
    UnsupportedConversion,

    /// <summary>
    /// Нужная внешняя программа не найдена.
    /// </summary>
    ToolNotFound,

    /// <summary>
    /// Внешняя программа завершилась с ошибкой.
    /// </summary>
    ToolFailed,

    /// <summary>
    /// Файл результата уже существует.
    /// </summary>
    FileExists,

    /// <summary>
    /// Вход не удалось прочитать.
    /// </summary>
    InputUnreadable,

    /// <summary>
    /// Движок расширения завершился непредвиденной ошибкой (исходное исключение — во внутренней части ошибки).
    /// </summary>
    EngineFailed,
}
