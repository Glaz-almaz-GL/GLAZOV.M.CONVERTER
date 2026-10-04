namespace GLAZOV.M.CONVERTER.Engines;

/// <summary>
/// Codecs of the first video and the first audio track of a file, as ffprobe names them; <c>null</c> for a missing track.
/// </summary>
/// <param name="Video">Video codec, for example <c>h264</c>.</param>
/// <param name="Audio">Audio codec, for example <c>aac</c>.</param>
/// <param name="VideoPixelFormat">Pixel format of the video track, for example <c>yuv420p</c>; <c>null</c> when unknown.</param>
internal sealed record TrackCodecs(string? Video, string? Audio, string? VideoPixelFormat = null);
