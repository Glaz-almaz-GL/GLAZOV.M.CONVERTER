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
        FileFormat sourceType,
        FileFormat targetFormat,
        TimeSpan duration,
        string? outputPath,
        byte[]? bytes,
        Stream? stream,
        bool reencoded = true,
        IReadOnlyList<string>? outputPaths = null,
        IReadOnlyList<string>? notes = null)
    {
        OutputPaths = outputPaths ?? (outputPath is null ? [] : [outputPath]);
        Notes = notes ?? [];
        Succeeded = succeeded;
        SourceType = sourceType;
        TargetFormat = targetFormat;
        Duration = duration;
        OutputPath = outputPath;
        Bytes = bytes;
        Stream = stream;
        Reencoded = reencoded;
    }

    /// <summary>
    /// Удалась ли конвертация.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Подтверждённый тип входа.
    /// </summary>
    public FileFormat SourceType { get; }

    /// <summary>
    /// Формат результата.
    /// </summary>
    public FileFormat TargetFormat { get; }

    /// <summary>
    /// Сколько заняла конвертация.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Путь к файлу результата, если результат записан в файл.
    /// </summary>
    public string? OutputPath { get; }

    /// <summary>
    /// Every file of the result: one for a file or a folder target, several when the pair writes several files to a folder, none for a stream or memory.
    /// </summary>
    public IReadOnlyList<string> OutputPaths { get; }

    /// <summary>
    /// Lines the engine added about this input, such as the sheet that was taken; empty when it had nothing to say.
    /// </summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>
    /// Данные результата, если он оставлен в памяти.
    /// </summary>
    public byte[]? Bytes { get; }

    /// <summary>
    /// Читаемый поток с результатом, если он оставлен в памяти.
    /// </summary>
    public Stream? Stream { get; }

    /// <summary>
    /// <c>true</c>, если хотя бы одна дорожка перекодирована; для звука и изображений всегда <c>true</c>.
    /// Значение задаёт библиотека.
    /// </summary>
    public bool Reencoded { get; }
}
