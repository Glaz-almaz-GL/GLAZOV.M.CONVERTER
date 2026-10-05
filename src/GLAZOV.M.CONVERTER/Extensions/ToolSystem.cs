namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Операционная система, для которой в <see cref="ToolSpec"/> заданы места поиска и подсказки установки.
/// </summary>
public enum ToolSystem
{
    /// <summary>
    /// Windows.
    /// </summary>
    Windows,

    /// <summary>
    /// macOS.
    /// </summary>
    MacOS,

    /// <summary>
    /// Linux и остальные системы семейства Unix.
    /// </summary>
    Linux,
}
