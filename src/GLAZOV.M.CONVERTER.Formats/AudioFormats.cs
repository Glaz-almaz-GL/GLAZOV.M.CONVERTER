using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// The audio formats of the standard set.
/// </summary>
public static class AudioFormats
{
    /// <summary>
    /// MP3.
    /// </summary>
    public static FileFormat Mp3 { get; } = FileFormat.Create("MP3", "audio/mpeg", "mp3");

    /// <summary>
    /// WAV.
    /// </summary>
    public static FileFormat Wav { get; } = FileFormat.Create("WAV", "audio/wav", "wav");

    /// <summary>
    /// OGG (Vorbis and Opus).
    /// </summary>
    public static FileFormat Ogg { get; } = FileFormat.Create("OGG", "audio/ogg", "ogg");

    /// <summary>
    /// FLAC, lossless audio.
    /// </summary>
    public static FileFormat Flac { get; } = FileFormat.Create("FLAC", "audio/flac", "flac");

    /// <summary>
    /// M4A, AAC audio in an MP4 container.
    /// </summary>
    public static FileFormat M4a { get; } = FileFormat.Create("M4A", "audio/mp4", "m4a");
}
