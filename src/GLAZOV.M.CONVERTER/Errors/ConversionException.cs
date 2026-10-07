namespace GLAZOV.M.CONVERTER.Errors;

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

    /// <summary>
    /// Creates the error for an external program that finished with a failure: stage <see cref="ConversionStage.Conversion"/>,
    /// reason <see cref="ConversionFailureReason.ToolFailed"/>, the program name (<see cref="ToolName"/>), its exit code (<see cref="ToolExitCode"/>)
    /// and the last lines of its output (<see cref="ToolOutput"/>).
    /// The message is the same as for the built-in engines.
    /// </summary>
    /// <param name="tool">Program name.</param>
    /// <param name="exitCode">Exit code of the program.</param>
    /// <param name="output">Output of the program; only the last lines are kept.</param>
    public static ConversionException ToolFailed(string tool, int exitCode, string? output)
    {
        return Failures.ToolFailed(tool, exitCode, output);
    }

    /// <summary>
    /// Creates the error for a call that asks for what the result format cannot hold: stage <see cref="ConversionStage.Conversion"/>,
    /// reason <see cref="ConversionFailureReason.InvalidParameters"/>, the given message.
    /// </summary>
    /// <param name="message">What is wrong and what to change, in plain words.</param>
    public static ConversionException InvalidParameters(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return Failures.InvalidParameters(message);
    }
}
