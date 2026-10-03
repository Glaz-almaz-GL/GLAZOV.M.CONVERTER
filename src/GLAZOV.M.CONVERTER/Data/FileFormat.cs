namespace GLAZOV.M.CONVERTER.Data;

/// <summary>
/// Формат результата конвертации. Задаётся всегда явно, по имени файла не определяется.
/// Тип расширяем: готовые значения — <see cref="Mp3"/>, <see cref="Wav"/>, <see cref="Ogg"/>, <see cref="Jpeg"/>,
/// свой формат создаётся через <see cref="Create"/>.
/// </summary>
public sealed class FileFormat : IEquatable<FileFormat>
{
    private FileFormat(string name, string mimeType, string extension)
    {
        Name = name;
        MimeType = mimeType;
        Extension = extension;
    }

    /// <summary>
    /// MP3.
    /// </summary>
    public static FileFormat Mp3 { get; } = new("MP3", "audio/mpeg", "mp3");

    /// <summary>
    /// WAV.
    /// </summary>
    public static FileFormat Wav { get; } = new("WAV", "audio/wav", "wav");

    /// <summary>
    /// OGG (Vorbis и Opus).
    /// </summary>
    public static FileFormat Ogg { get; } = new("OGG", "audio/ogg", "ogg");

    /// <summary>
    /// JPEG (изображения — следующие версии).
    /// </summary>
    public static FileFormat Jpeg { get; } = new("JPEG", "image/jpeg", "jpg");

    /// <summary>
    /// FLAC — звук без потерь.
    /// </summary>
    public static FileFormat Flac { get; } = new("FLAC", "audio/flac", "flac");

    /// <summary>
    /// M4A — звук AAC в контейнере MP4.
    /// </summary>
    public static FileFormat M4a { get; } = new("M4A", "audio/mp4", "m4a");

    /// <summary>
    /// PNG — изображение без потерь.
    /// </summary>
    public static FileFormat Png { get; } = new("PNG", "image/png", "png");

    /// <summary>
    /// WebP.
    /// </summary>
    public static FileFormat Webp { get; } = new("WebP", "image/webp", "webp");

    /// <summary>
    /// BMP.
    /// </summary>
    public static FileFormat Bmp { get; } = new("BMP", "image/bmp", "bmp");

    /// <summary>
    /// GIF; берётся первый кадр, анимация не сохраняется.
    /// </summary>
    public static FileFormat Gif { get; } = new("GIF", "image/gif", "gif");

    /// <summary>
    /// TIFF.
    /// </summary>
    public static FileFormat Tiff { get; } = new("TIFF", "image/tiff", "tiff");

    /// <summary>
    /// TGA — только результат: по содержимому этот тип определить нельзя (в начале файла нет сигнатуры).
    /// </summary>
    public static FileFormat Tga { get; } = new("TGA", "image/x-tga", "tga");

    /// <summary>
    /// DDS — пока только вход.
    /// </summary>
    public static FileFormat Dds { get; } = new("DDS", "image/vnd.ms-dds", "dds");

    /// <summary>
    /// PSD — пока только вход.
    /// </summary>
    public static FileFormat Psd { get; } = new("PSD", "image/vnd.adobe.photoshop", "psd");

    /// <summary>
    /// SVG — вход; растеризуется отдельным проектом-расширением.
    /// </summary>
    public static FileFormat Svg { get; } = new("SVG", "image/svg+xml", "svg");

    /// <summary>
    /// Тип не распознан: результат определения типа по начальным байтам. Целью конвертации быть не может.
    /// </summary>
    public static FileFormat Unknown { get; } = new("Unknown", "application/octet-stream", string.Empty);

    /// <summary>
    /// Короткое имя формата.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// MIME-тип.
    /// </summary>
    public string MimeType { get; }

    /// <summary>
    /// Расширение имени файла без точки, в нижнем регистре.
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Создаёт свой формат — для типа файла, которого нет среди готовых.
    /// </summary>
    /// <param name="name">Короткое имя, например «FLAC».</param>
    /// <param name="mimeType">MIME-тип, например «audio/flac».</param>
    /// <param name="extension">Расширение имени файла; ведущая точка допускается и отбрасывается.</param>
    public static FileFormat Create(string name, string mimeType, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        string normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
        if (normalized.Length == 0 || normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || normalized.Contains('.'))
        {
            throw new ArgumentException(
                $"Расширение «{extension}» не подходит: нужны буквы и цифры без точек и разделителей путей.",
                nameof(extension));
        }

        return new FileFormat(name.Trim(), mimeType.Trim(), normalized);
    }

    /// <inheritdoc />
    public bool Equals(FileFormat? other)
    {
        return other is not null
            && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(MimeType, other.MimeType, StringComparison.OrdinalIgnoreCase)
            && Extension == other.Extension;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as FileFormat);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Name.ToUpperInvariant(), MimeType.ToUpperInvariant(), Extension);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
