using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;

namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Итог одиночной конвертации.
/// </summary>
public sealed class ConversionResult
{
    internal ConversionResult(
        bool succeeded,
        FileType sourceType,
        Format targetFormat,
        TimeSpan duration,
        string? outputPath,
        byte[]? bytes,
        Stream? stream)
    {
        Succeeded = succeeded;
        SourceType = sourceType;
        TargetFormat = targetFormat;
        Duration = duration;
        OutputPath = outputPath;
        Bytes = bytes;
        Stream = stream;
    }

    /// <summary>
    /// Удалась ли конвертация.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Подтверждённый тип входа.
    /// </summary>
    public FileType SourceType { get; }

    /// <summary>
    /// Формат результата.
    /// </summary>
    public Format TargetFormat { get; }

    /// <summary>
    /// Сколько заняла конвертация.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Путь к файлу результата, если результат записан в файл.
    /// </summary>
    public string? OutputPath { get; }

    /// <summary>
    /// Данные результата, если он оставлен в памяти.
    /// </summary>
    public byte[]? Bytes { get; }

    /// <summary>
    /// Читаемый поток с результатом, если он оставлен в памяти.
    /// </summary>
    public Stream? Stream { get; }
}
