using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// The video containers of the standard set.
/// </summary>
public static class VideoFormats
{
    /// <summary>
    /// MP4. Also covers an MP4 container with no video track (audio with common brands other than M4A).
    /// </summary>
    public static FileFormat Mp4 { get; } = FileFormat.Create("MP4", "video/mp4", "mp4");

    /// <summary>
    /// MKV (Matroska).
    /// </summary>
    public static FileFormat Mkv { get; } = FileFormat.Create("MKV", "video/x-matroska", "mkv");

    /// <summary>
    /// WebM.
    /// </summary>
    public static FileFormat Webm { get; } = FileFormat.Create("WebM", "video/webm", "webm");

    /// <summary>
    /// MOV (QuickTime).
    /// </summary>
    public static FileFormat Mov { get; } = FileFormat.Create("MOV", "video/quicktime", "mov");

    /// <summary>
    /// AVI.
    /// </summary>
    public static FileFormat Avi { get; } = FileFormat.Create("AVI", "video/x-msvideo", "avi");
}
