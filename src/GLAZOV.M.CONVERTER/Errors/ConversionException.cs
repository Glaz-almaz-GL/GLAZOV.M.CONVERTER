namespace GLAZOV.M.CONVERTER;

/// <summary>
/// Ошибка одиночной конвертации: этап, причина и понятное сообщение. Отмена ошибкой не считается.
/// </summary>
public sealed class ConversionException : Exception
{
    internal ConversionException(
        ConversionStage stage,
        ConversionFailureReason reason,
        string message,
        string? toolName = null,
        int? toolExitCode = null,
        string? toolOutput = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Stage = stage;
        Reason = reason;
        ToolName = toolName;
        ToolExitCode = toolExitCode;
        ToolOutput = toolOutput;
    }

    /// <summary>
    /// Этап, на котором случилась ошибка.
    /// </summary>
    public ConversionStage Stage { get; }

    /// <summary>
    /// Причина ошибки.
    /// </summary>
    public ConversionFailureReason Reason { get; }

    /// <summary>
    /// Имя внешней программы, если причина связана с ней.
    /// </summary>
    public string? ToolName { get; }

    /// <summary>
    /// Код выхода внешней программы, когда причина — <see cref="ConversionFailureReason.ToolFailed"/>.
    /// </summary>
    public int? ToolExitCode { get; }

    /// <summary>
    /// Последние строки сообщения внешней программы, когда причина — <see cref="ConversionFailureReason.ToolFailed"/>.
    /// </summary>
    public string? ToolOutput { get; }
}
