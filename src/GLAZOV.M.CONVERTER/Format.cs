namespace GLAZOV.M.CONVERTER;

/// <summary>Формат результата конвертации. Задаётся всегда явно, по имени файла не определяется.</summary>
public enum Format
{
    /// <summary>MP3.</summary>
    Mp3,

    /// <summary>WAV.</summary>
    Wav,

    /// <summary>OGG (Vorbis и Opus).</summary>
    Ogg,

    /// <summary>JPEG (изображения — следующие версии).</summary>
    Jpeg,
}
