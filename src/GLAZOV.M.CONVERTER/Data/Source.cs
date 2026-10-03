namespace GLAZOV.M.CONVERTER.Data;

/// <summary>
/// Откуда берутся данные: файл, поток или папка с файлами.
/// </summary>
public sealed class Source
{
    private Source(SourceKind kind, string? path, Stream? stream, string pattern, bool recursive)
    {
        Kind = kind;
        Path = path;
        InputStream = stream;
        Pattern = pattern;
        Recursive = recursive;
    }

    internal SourceKind Kind { get; }

    internal string? Path { get; }

    internal Stream? InputStream { get; }

    internal string Pattern { get; }

    internal bool Recursive { get; }

    /// <summary>
    /// Один файл на диске.
    /// </summary>
    /// <param name="path">Путь к файлу.</param>
    public static Source File(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new Source(SourceKind.File, path, null, "*", false);
    }

    /// <summary>
    /// Поток с данными файла целиком.
    /// </summary>
    /// <param name="stream">Читаемый поток.</param>
    public static Source Stream(System.IO.Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new Source(SourceKind.Stream, null, stream, "*", false);
    }

    /// <summary>
    /// Папка с файлами (для пакетной обработки).
    /// </summary>
    /// <param name="path">Путь к папке.</param>
    /// <param name="pattern">Маска имён файлов.</param>
    /// <param name="recursive">Входить ли во вложенные папки.</param>
    public static Source Folder(string path, string pattern = "*", bool recursive = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        return new Source(SourceKind.Folder, path, null, pattern, recursive);
    }
}
