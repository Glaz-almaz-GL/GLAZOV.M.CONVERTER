namespace GLAZOV.M.CONVERTER.Tooling;

/// <summary>
/// Итог запуска: код выхода и последние строки сообщений об ошибках программы.
/// Код выхода не ноль — не исключение, решает вызывающий.
/// </summary>
/// <param name="ExitCode">Код выхода.</param>
/// <param name="ErrorOutput">Последние строки стандартного потока ошибок (не больше <see cref="ToolRunner.KeptErrorLines"/>).</param>
internal sealed record ToolResult(int ExitCode, string ErrorOutput);
