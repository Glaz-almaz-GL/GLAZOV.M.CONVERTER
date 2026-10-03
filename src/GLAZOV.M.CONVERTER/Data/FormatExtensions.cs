namespace GLAZOV.M.CONVERTER.Data;

/// <summary>
/// Расширение имени файла по формату результата.
/// </summary>
internal static class FormatExtensions
{
    /// <summary>
    /// Расширение без точки: <c>mp3</c>, <c>wav</c>, <c>ogg</c>, <c>jpg</c>.
    /// </summary>
    /// <param name="format">Формат результата.</param>
    internal static string FileExtension(this Format format) => format switch
    {
        Format.Mp3 => "mp3",
        Format.Wav => "wav",
        Format.Ogg => "ogg",
        Format.Jpeg => "jpg",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Для этого формата расширение не определено."),
    };
}
