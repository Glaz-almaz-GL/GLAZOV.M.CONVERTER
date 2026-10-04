using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Which codecs a track may be copied with into each container. One table, in one place; every row is checked by
/// a real copy with the installed FFmpeg, and a row FFmpeg refuses is removed. Matroska takes any codec.
/// </summary>
internal static class ContainerCodecs
{
    private static readonly Dictionary<string, string[]> Video = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MP4"] = ["h264", "hevc", "mpeg4", "av1", "vp9"],
        ["MOV"] = ["h264", "hevc", "mpeg4"],
        ["WebM"] = ["vp8", "vp9", "av1"],
        ["AVI"] = ["mpeg4", "mjpeg"],
    };

    private static readonly Dictionary<string, string[]> Audio = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MP4"] = ["aac", "mp3", "ac3"],
        ["MOV"] = ["aac", "mp3", "ac3"],
        ["WebM"] = ["opus", "vorbis"],
        ["AVI"] = ["mp3", "ac3", "pcm_s16le"],
    };

    /// <summary>
    /// Whether a video track of this codec may be copied into the container.
    /// </summary>
    /// <param name="to">Target container.</param>
    /// <param name="codec">Codec name as ffprobe prints it.</param>
    internal static bool AllowsVideo(FileFormat to, string codec)
    {
        return Allows(Video, to, codec);
    }

    /// <summary>
    /// Whether an audio track of this codec may be copied into the container.
    /// </summary>
    /// <param name="to">Target container.</param>
    /// <param name="codec">Codec name as ffprobe prints it.</param>
    internal static bool AllowsAudio(FileFormat to, string codec)
    {
        return Allows(Audio, to, codec);
    }

    /// <summary>
    /// The codec names that have a row for the container, for checking the table against real copies.
    /// </summary>
    /// <param name="to">Target container.</param>
    internal static (IReadOnlyList<string> Video, IReadOnlyList<string> Audio) RowsOf(FileFormat to)
    {
        return (Video.GetValueOrDefault(to.Name) ?? [], Audio.GetValueOrDefault(to.Name) ?? []);
    }

    private static bool Allows(Dictionary<string, string[]> table, FileFormat to, string codec)
    {
        if (to.Equals(FileFormat.Mkv))
        {
            return true;
        }

        return table.TryGetValue(to.Name, out string[]? codecs) && codecs.Contains(codec, StringComparer.OrdinalIgnoreCase);
    }
}
