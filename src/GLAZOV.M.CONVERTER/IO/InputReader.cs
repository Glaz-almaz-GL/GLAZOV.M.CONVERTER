using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;

namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// Единая точка открытия входа: файл, поток или папка. Любая ошибка доступа превращается
/// в <see cref="ConversionException"/> (этап «подготовка», причина «вход не прочитан»).
/// </summary>
internal static class InputReader
{
    /// <summary>
    /// Имя входа-потока в отчётах.
    /// </summary>
    internal const string StreamName = "поток";

    /// <summary>
    /// Имя входа для отчёта и имени результата: имя файла или «поток»; папка — имя папки.
    /// </summary>
    /// <param name="source">Вход.</param>
    internal static string NameOf(Source source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Kind == SourceKind.Stream ? StreamName : Path.GetFileName(source.Path!);
    }

    /// <summary>
    /// Открывает файл или поток для чтения.
    /// </summary>
    /// <param name="source">Файл или поток; папка — неверное использование.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal static ValueTask<InputHandle> OpenAsync(Source source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        return source.Kind switch
        {
            SourceKind.File => ValueTask.FromResult(OpenFile(source.Path!)),
            SourceKind.Stream => ValueTask.FromResult(OpenStream(source.InputStream!)),
            _ => throw new ArgumentException("Папку нельзя открыть как один вход: для пакета используйте пакетную конвертацию.", nameof(source)),
        };
    }

    /// <summary>
    /// Перечисляет файлы папки по маске и рекурсии из <see cref="Source.Folder"/>; порядок стабильный — по имени.
    /// </summary>
    /// <param name="folder">Источник-папка.</param>
    internal static IReadOnlyList<Source> Expand(Source folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (folder.Kind != SourceKind.Folder)
        {
            throw new ArgumentException("Ожидалась папка (Source.Folder).", nameof(folder));
        }

        string path = folder.Path!;
        try
        {
            SearchOption option = folder.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return [.. Directory.EnumerateFiles(path, folder.Pattern, option)
                .Order(StringComparer.Ordinal)
                .Select(Source.File)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Failures.InputUnreadable(path, ex);
        }
    }

    private static InputHandle OpenFile(string path)
    {
        try
        {
            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new InputHandle(Path.GetFileName(path), stream, ownsInner: true, Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw Failures.InputUnreadable(Path.GetFileName(path), ex);
        }
    }

    private static InputHandle OpenStream(Stream stream)
    {
        return !stream.CanRead
            ? throw Failures.InputUnreadable(StreamName, "поток не поддерживает чтение.")
            : new InputHandle(StreamName, stream, ownsInner: false);
    }
}
