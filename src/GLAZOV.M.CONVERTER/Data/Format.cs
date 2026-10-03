namespace GLAZOV.M.CONVERTER.Data;

/// <summary>
/// Формат результата конвертации. Задаётся всегда явно, по имени файла не определяется.
/// Тип расширяем: готовые значения — <see cref="Mp3"/>, <see cref="Wav"/>, <see cref="Ogg"/>, <see cref="Jpeg"/>,
/// свой формат создаётся через <see cref="Create"/>.
/// </summary>
public sealed class Format : IEquatable<Format>
{
    private Format(string name, string mimeType, string extension)
    {
        Name = name;
        MimeType = mimeType;
        Extension = extension;
    }

    /// <summary>
    /// MP3.
    /// </summary>
    public static Format Mp3 { get; } = new("MP3", "audio/mpeg", "mp3");

    /// <summary>
    /// WAV.
    /// </summary>
    public static Format Wav { get; } = new("WAV", "audio/wav", "wav");

    /// <summary>
    /// OGG (Vorbis и Opus).
    /// </summary>
    public static Format Ogg { get; } = new("OGG", "audio/ogg", "ogg");

    /// <summary>
    /// JPEG (изображения — следующие версии).
    /// </summary>
    public static Format Jpeg { get; } = new("JPEG", "image/jpeg", "jpg");

    /// <summary>
    /// Тип не распознан: результат определения типа по начальным байтам. Целью конвертации быть не может.
    /// </summary>
    public static Format Unknown { get; } = new("Unknown", "application/octet-stream", string.Empty);

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
    public static Format Create(string name, string mimeType, string extension)
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

        return new Format(name.Trim(), mimeType.Trim(), normalized);
    }

    /// <inheritdoc />
    public bool Equals(Format? other)
    {
        return other is not null
            && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(MimeType, other.MimeType, StringComparison.OrdinalIgnoreCase)
            && Extension == other.Extension;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as Format);
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
