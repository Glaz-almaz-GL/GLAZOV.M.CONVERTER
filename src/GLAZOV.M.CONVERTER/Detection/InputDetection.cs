using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.IO;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Определение типа файла или потока по начальным байтам рядом определителей конвертера. Расширение имени не учитывается.
/// </summary>
internal static class InputDetection
{
    /// <summary>
    /// Определяет тип файла или потока рядом определителей (готовые и расширений конвертера).
    /// </summary>
    /// <param name="source">Файл или поток.</param>
    /// <param name="chain">Ряд определителей.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal static async Task<FileFormat> DetectAsync(Source source, DetectorChain chain, CancellationToken cancellationToken)
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
            return await DetectAsync(handle, chain, cancellationToken).ConfigureAwait(false);
        }
        catch (ConversionException ex) when (ex.Stage != ConversionStage.Detection)
        {
            // Вход не открылся: для вызывающего это сбой на этапе определения типа.
            throw new ConversionException(ConversionStage.Detection, ex.Reason, ex.Message, innerException: ex);
        }
    }

    /// <summary>
    /// Определяет тип по уже открытому входу рядом определителей (готовые и расширений конвертера).
    /// </summary>
    /// <param name="handle">Открытый вход.</param>
    /// <param name="chain">Ряд определителей.</param>
    /// <param name="cancellationToken">Отмена.</param>
    internal static async Task<FileFormat> DetectAsync(InputHandle handle, DetectorChain chain, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(chain);
        ReadOnlyMemory<byte> head = await handle.HeadAsync(chain.HeadLength, cancellationToken).ConfigureAwait(false);
        return await chain.DetectAsync(head, handle, cancellationToken).ConfigureAwait(false);
    }
}
