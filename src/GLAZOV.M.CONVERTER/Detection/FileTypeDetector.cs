using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.IO;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Определение типа файла или потока по начальным байтам. Расширение имени не учитывается.
/// </summary>
public static class FileTypeDetector
{
    /// <summary>
    /// Определяет тип без конвертации; тот же код библиотека вызывает перед каждой конвертацией.
    /// Поток, умеющий искать, после определения возвращается на исходную позицию.
    /// Поток, который нельзя перематывать, не принимается: прочитанные байты были бы потеряны.
    /// </summary>
    /// <param name="source">Файл или поток.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <exception cref="ArgumentException">Папка вместо файла или поток без перемотки.</exception>
    /// <exception cref="ConversionException">Вход не удалось прочитать (этап «определение типа»).</exception>
    public static async Task<FileFormat> DetectAsync(Source source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Kind == SourceKind.Folder)
        {
            throw new ArgumentException("Тип определяется у файла или потока, а не у папки.", nameof(source));
        }

        if (source.Kind == SourceKind.Stream && !source.InputStream!.CanSeek)
        {
            throw new ArgumentException(
                "Поток нельзя перематывать: определение типа прочитало бы его начало без возврата. Передайте поток с перемоткой или файл.",
                nameof(source));
        }

        try
        {
            await using InputHandle handle = await InputReader.OpenAsync(source, cancellationToken).ConfigureAwait(false);
            return await DetectAsync(handle, cancellationToken).ConfigureAwait(false);
        }
        catch (ConversionException ex) when (ex.Stage != ConversionStage.Detection)
        {
            // Вход не открылся: для вызывающего это сбой на этапе определения типа.
            throw new ConversionException(ConversionStage.Detection, ex.Reason, ex.Message, innerException: ex);
        }
    }

    /// <summary>
    /// Определяет тип по уже открытому входу готовыми признаками — его начало остаётся читаемым для конвертации.
    /// </summary>
    /// <param name="handle">Открытый вход.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal static Task<FileFormat> DetectAsync(InputHandle handle, CancellationToken cancellationToken = default)
    {
        return DetectAsync(handle, DetectorChain.BuiltIn, cancellationToken);
    }

    /// <summary>
    /// Определяет тип по уже открытому входу цепочкой определителей (готовые и расширений конвертера).
    /// </summary>
    /// <param name="handle">Открытый вход.</param>
    /// <param name="chain">Цепочка определителей.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal static async Task<FileFormat> DetectAsync(InputHandle handle, DetectorChain chain, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(chain);
        ReadOnlyMemory<byte> head = await handle.HeadAsync(chain.HeadLength, cancellationToken).ConfigureAwait(false);
        FileFormat found = chain.Detect(head.Span);
        if (!found.Equals(FileFormat.Unknown) || !handle.CanProbe || !ContainerProbe.IsContainer(head.Span))
        {
            return found;
        }

        // The leading bytes did not settle a container; its directory may lie anywhere, so it is read where it is.
        FileFormat? probed = await Task.Run(() => handle.Probe(chain.ContainerProbeLimit, ContainerProbe.Match), cancellationToken).ConfigureAwait(false);
        return probed ?? FileFormat.Unknown;
    }
}
