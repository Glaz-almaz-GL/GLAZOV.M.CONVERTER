namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Тип файла или потока, подтверждённый по начальным байтам.
/// </summary>
public sealed class FileType
{
    private FileType(string name, string mimeType, string extension)
    {
        Name = name;
        MimeType = mimeType;
        Extension = extension;
    }

    /// <summary>
    /// MP3.
    /// </summary>
    public static FileType Mp3 { get; } = new("MP3", "audio/mpeg", "mp3");

    /// <summary>
    /// WAV.
    /// </summary>
    public static FileType Wav { get; } = new("WAV", "audio/wav", "wav");

    /// <summary>
    /// OGG.
    /// </summary>
    public static FileType Ogg { get; } = new("OGG", "audio/ogg", "ogg");

    /// <summary>
    /// Тип не распознан.
    /// </summary>
    public static FileType Unknown { get; } = new("Unknown", "application/octet-stream", string.Empty);

    /// <summary>
    /// Короткое имя типа.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// MIME-тип.
    /// </summary>
    public string MimeType { get; }

    /// <summary>
    /// Обычное расширение имени файла без точки; у неизвестного типа пустое.
    /// </summary>
    public string Extension { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
