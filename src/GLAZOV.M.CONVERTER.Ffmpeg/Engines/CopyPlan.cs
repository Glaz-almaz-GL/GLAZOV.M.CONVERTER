using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats.Options;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Engines;

/// <summary>
/// What is done with each track: copied as it is, or encoded. The two tracks are decided separately.
/// </summary>
/// <param name="CopyVideo">The video track is copied.</param>
/// <param name="CopyAudio">The audio track is copied.</param>
internal sealed record CopyPlan(bool CopyVideo, bool CopyAudio)
{
    /// <summary>
    /// Everything is encoded.
    /// </summary>
    internal static CopyPlan EncodeAll { get; } = new(false, false);

    /// <summary>
    /// Decides per track. Video is copied when nothing asks to change it (no video settings, no actions, no
    /// <see cref="VideoOptions.Reencode"/>, and no <see cref="VideoOptions.ConvertPixelFormat"/> for an unusual format) and its codec fits the container; audio when no audio settings
    /// are set and its codec fits. Unknown codecs (<paramref name="codecs"/> is <c>null</c>) mean encoding.
    /// </summary>
    /// <param name="to">Target container.</param>
    /// <param name="video">Video settings.</param>
    /// <param name="audio">Audio settings.</param>
    /// <param name="hasOperations">Whether actions (rotate, scale) are asked for.</param>
    /// <param name="codecs">Codecs of the input tracks.</param>
    internal static CopyPlan Decide(FileFormat to, VideoOptions? video, AudioOptions? audio, bool hasOperations, TrackCodecs? codecs)
    {
        if (codecs is null || video?.Reencode == true)
        {
            return EncodeAll;
        }

        bool copyVideo = codecs.Video is not null && !hasOperations && !VideoChanged(video) && ContainerCodecs.AllowsVideo(to, codecs.Video)
            && !(video?.ConvertPixelFormat == true && IsUnusualPixelFormat(codecs.VideoPixelFormat));
        bool copyAudio = codecs.Audio is not null && !AudioChanged(audio) && ContainerCodecs.AllowsAudio(to, codecs.Audio);
        return new CopyPlan(copyVideo, copyAudio);
    }

    /// <summary>
    /// Whether the result carries no encoded track: every track the input has is copied.
    /// </summary>
    /// <param name="codecs">Codecs of the input tracks.</param>
    internal bool CopiesEverything(TrackCodecs? codecs)
    {
        return codecs is not null
            && (codecs.Video is null || CopyVideo)
            && (codecs.Audio is null || CopyAudio);
    }

    // Common 8-bit 4:2:0 (the "j" variant is the same layout with full range); an unknown format is not touched.
    private static bool IsUnusualPixelFormat(string? pixelFormat)
    {
        return pixelFormat is not null && pixelFormat is not ("yuv420p" or "yuvj420p");
    }

    private static bool VideoChanged(VideoOptions? video)
    {
        return video is { Quality: not null } or { Bitrate: not null } or { Width: not null } or { Height: not null } or { FrameRate: not null };
    }

    private static bool AudioChanged(AudioOptions? audio)
    {
        return audio is { Bitrate: not null } or { SampleRate: not null } or { Channels: not null };
    }
}
