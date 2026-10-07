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

    /// <summary>
    /// The result cannot be written: no access, no space on the disk, a path that cannot be created or a target stream that failed.
    /// </summary>
    OutputUnwritable,

    /// <summary>
    /// The call asks for something the result format cannot hold (for example an ICO side above 256 pixels): the input and the
    /// program are fine, the parameters are not. The message names the limit and what to change.
    /// </summary>
    InvalidParameters,
}
