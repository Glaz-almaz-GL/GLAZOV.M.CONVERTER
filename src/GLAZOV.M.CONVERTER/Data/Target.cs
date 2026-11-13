using GLAZOV.M.CONVERTER.Converting;

namespace GLAZOV.M.CONVERTER.Data;

/// <summary>
/// Куда кладётся результат: файл, поток, память или папка. Формат задаётся всегда явно.
/// </summary>
public sealed class Target
{
    private Target(TargetKind kind, string? path, Stream? stream, FileFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (format.Equals(FileFormat.Unknown))
        {
            throw new ArgumentException("Формат \"Unknown\" означает \"тип не распознан\" и не может быть целью конвертации.", nameof(format));
        }

        Kind = kind;
        Path = path;
        OutputStream = stream;
        FileFormat = format;
    }

    internal TargetKind Kind { get; init; }

    internal string? Path { get; init; }

    internal Stream? OutputStream { get; init; }

    internal FileFormat FileFormat { get; init; }

    /// <summary>
    /// Файл на диске.
    /// </summary>
    /// <param name="path">Путь к файлу результата.</param>
    /// <param name="format">Формат результата.</param>
    public static Target File(string path, FileFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new Target(TargetKind.File, path, null, format);
    }

    /// <summary>
    /// Записываемый поток.
    /// </summary>
    /// <param name="stream">Поток, в который пишется результат.</param>
    /// <param name="format">Формат результата.</param>
    public static Target Stream(Stream stream, FileFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new Target(TargetKind.Stream, null, stream, format);
    }

    /// <summary>
    /// Результат остаётся в памяти и возвращается в <see cref="ConversionResult"/>.
    /// </summary>
    /// <param name="format">Формат результата.</param>
    public static Target Memory(FileFormat format)
    {
        return new(TargetKind.Memory, null, null, format);
    }

    /// <summary>
    /// Папка для результатов пакета.
    /// </summary>
    /// <param name="path">Путь к папке.</param>
    /// <param name="format">Формат результатов.</param>
    public static Target Folder(string path, FileFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new Target(TargetKind.Folder, path, null, format);
    }
}
