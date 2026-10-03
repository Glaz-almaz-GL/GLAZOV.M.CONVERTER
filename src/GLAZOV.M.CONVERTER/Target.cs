namespace GLAZOV.M.CONVERTER;

/// <summary>Куда кладётся результат: файл, поток, память или папка. Формат задаётся всегда явно.</summary>
public sealed class Target
{
    private Target(TargetKind kind, string? path, System.IO.Stream? stream, Format format)
    {
        Kind = kind;
        Path = path;
        OutputStream = stream;
        Format = format;
    }

    internal TargetKind Kind { get; }

    internal string? Path { get; }

    internal System.IO.Stream? OutputStream { get; }

    internal Format Format { get; }

    /// <summary>Файл на диске.</summary>
    /// <param name="path">Путь к файлу результата.</param>
    /// <param name="format">Формат результата.</param>
    public static Target File(string path, Format format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new Target(TargetKind.File, path, null, format);
    }

    /// <summary>Записываемый поток.</summary>
    /// <param name="stream">Поток, в который пишется результат.</param>
    /// <param name="format">Формат результата.</param>
    public static Target Stream(System.IO.Stream stream, Format format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new Target(TargetKind.Stream, null, stream, format);
    }

    /// <summary>Результат остаётся в памяти и возвращается в <see cref="ConversionResult"/>.</summary>
    /// <param name="format">Формат результата.</param>
    public static Target Memory(Format format) => new(TargetKind.Memory, null, null, format);

    /// <summary>Папка для результатов пакета.</summary>
    /// <param name="path">Путь к папке.</param>
    /// <param name="format">Формат результатов.</param>
    public static Target Folder(string path, Format format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new Target(TargetKind.Folder, path, null, format);
    }
}

internal enum TargetKind
{
    File,
    Stream,
    Memory,
    Folder,
}
