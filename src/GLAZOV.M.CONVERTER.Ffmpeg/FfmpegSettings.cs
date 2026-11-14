using System.Drawing;

namespace GLAZOV.M.CONVERTER.Ffmpeg;

/// <summary>
/// Settings of the FFmpeg engines. Passed to <see cref="FfmpegExtension"/> on creation;
/// the extension takes a copy, so changing the object afterwards does not change the extension.
/// </summary>
/// <remarks>
/// Every value here is only "what to take when the call does not say": an explicit parameter of the call
/// (<c>AudioOptions</c>, <c>ImageOptions</c>, <c>VideoOptions</c>) always wins.
/// </remarks>
public sealed class FfmpegSettings
{
    /// <summary>
    /// MP3 quality (VBR scale of LAME, 0 is the best, 9 the worst) when the call sets no bitrate.
    /// Also used for the sound of an AVI result. Default 2.
    /// </summary>
    public int Mp3Quality
    {
        get;
        set => field = CheckRange(value, 0, 9, nameof(Mp3Quality));
    } = 2;

    /// <summary>
    /// OGG (Vorbis) quality from 0 to 10, higher is better, when the call sets no bitrate. Default 5.
    /// </summary>
    public int OggQuality
    {
        get;
        set => field = CheckRange(value, 0, 10, nameof(OggQuality));
    } = 5;

    /// <summary>
    /// FLAC compression level from 0 to 12: it changes size and speed, never the sound. Default 5.
    /// </summary>
    public int FlacCompression
    {
        get;
        set => field = CheckRange(value, 0, 12, nameof(FlacCompression));
    } = 5;

    /// <summary>
    /// AAC bitrate in bits per second when the call sets none. Used for M4A
    /// and for the sound of an MP4, MOV or MKV result. Default 128000.
    /// </summary>
    public int AacBitrate
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 128_000;

    /// <summary>
    /// JPEG quality from 1 to 100 when <c>ImageOptions.Quality</c> is not set. Default 90.
    /// </summary>
    public int JpegQuality
    {
        get;
        set => field = CheckRange(value, 1, 100, nameof(JpegQuality));
    } = 90;

    /// <summary>
    /// WebP quality from 1 to 100 when <c>ImageOptions.Quality</c> is not set. Default 80.
    /// </summary>
    public int WebpQuality
    {
        get;
        set => field = CheckRange(value, 1, 100, nameof(WebpQuality));
    } = 80;

    /// <summary>
    /// AVIF quality from 1 to 100 when <c>ImageOptions.Quality</c> is not set. Default 60.
    /// </summary>
    public int AvifQuality
    {
        get;
        set => field = CheckRange(value, 1, 100, nameof(AvifQuality));
    } = 60;

    /// <summary>
    /// Colour put under transparent places when the result is JPEG or AVIF, which keep no transparency here.
    /// The transparency of the colour itself is ignored. Default white.
    /// </summary>
    public Color FlattenBackground { get; set; } = Color.White;

    /// <summary>
    /// CRF of H.264 (MP4, MOV, MKV) from 0 to 51, lower is better, when <c>VideoOptions</c> sets neither quality nor bitrate. Default 23.
    /// </summary>
    public int H264Crf
    {
        get;
        set => field = CheckRange(value, 0, 51, nameof(H264Crf));
    } = 23;

    /// <summary>
    /// CRF of VP9 (WebM) from 0 to 63, lower is better, when <c>VideoOptions</c> sets neither quality nor bitrate. Default 31.
    /// </summary>
    public int Vp9Crf
    {
        get;
        set => field = CheckRange(value, 0, 63, nameof(Vp9Crf));
    } = 31;

    /// <summary>
    /// Quantiser scale of MPEG-4 (AVI) from 1 to 31, lower is better, when <c>VideoOptions</c> sets neither quality nor bitrate. Default 5.
    /// </summary>
    public int Mpeg4Qscale
    {
        get;
        set => field = CheckRange(value, 1, 31, nameof(Mpeg4Qscale));
    } = 5;

    /// <summary>
    /// VP9 encoding effort: <c>best</c> (smallest file, slowest), <c>good</c> or <c>realtime</c> (fastest). Default <c>good</c>.
    /// </summary>
    public string Vp9Deadline
    {
        get;
        set
        {
            if (value is not ("best" or "good" or "realtime"))
            {
                throw new ArgumentOutOfRangeException(nameof(Vp9Deadline), value, "Усилие кодировщика VP9 — «best», «good» или «realtime».");
            }

            field = value;
        }
    } = "good";

    /// <summary>
    /// VP9 speed step from 0 (slowest, best) to 8 (fastest), used together with <see cref="Vp9Deadline"/>. Default 4.
    /// </summary>
    public int Vp9CpuUsed
    {
        get;
        set => field = CheckRange(value, 0, 8, nameof(Vp9CpuUsed));
    } = 4;

    internal FfmpegSettings Copy()
    {
        return (FfmpegSettings)MemberwiseClone();
    }

    private static int CheckRange(int value, int min, int max, string name)
    {
        return value < min || value > max
            ? throw new ArgumentOutOfRangeException(name, value, $"Значение — число от {min} до {max}.")
            : value;
    }
}
