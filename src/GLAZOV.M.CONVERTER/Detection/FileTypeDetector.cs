using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Internal;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Определение типа файла или потока по начальным байтам. Расширение имени не учитывается.
/// </summary>
public static class FileTypeDetector
{
    /// <summary>
    /// Определяет тип без конвертации; тот же код библиотека вызывает перед каждой конвертацией.
    /// </summary>
    /// <param name="source">Файл или поток.</param>
    /// <param name="cancellationToken">Отмена.</param>
    public static Task<Format> DetectAsync(Source source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Task.FromException<Format>(Stub.NotYet("FileTypeDetector.DetectAsync"));
    }
}
