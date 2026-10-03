namespace GLAZOV.M.CONVERTER;

/// <summary>Определение типа файла или потока по начальным байтам. Расширение имени не учитывается.</summary>
public static class FileTypeDetector
{
    /// <summary>Определяет тип без конвертации; тот же код библиотека вызывает перед каждой конвертацией.</summary>
    /// <param name="source">Файл или поток.</param>
    /// <param name="cancellationToken">Отмена.</param>
    public static Task<FileType> DetectAsync(Source source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Task.FromException<FileType>(Stub.NotYet("FileTypeDetector.DetectAsync"));
    }
}
