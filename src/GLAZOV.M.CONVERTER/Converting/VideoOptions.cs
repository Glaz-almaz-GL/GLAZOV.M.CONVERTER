namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Video settings for one conversion call. All optional: not set means the engine default for the target format.
/// <see cref="Bitrate"/> replaces <see cref="Quality"/> when both are set.
/// <see cref="Width"/> without <see cref="Height"/> (and the other way round) keeps the proportions;
/// sizes are rounded down to even numbers.
/// </summary>
public sealed class VideoOptions
{
    private int? _quality;
    private int? _bitrate;
    private int? _width;
    private int? _height;
    private double? _frameRate;

    /// <summary>
    /// Quality from 1 (lowest) to 100 (highest). Replaced by <see cref="Bitrate"/> when that is set.
    /// </summary>
    public int? Quality
    {
        get => _quality;
        set
        {
            if (value is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(Quality), value, "Качество видео — число от 1 до 100 или не задано.");
            }

            _quality = value;
        }
    }

    /// <summary>
    /// Video bitrate, bits per second. Replaces <see cref="Quality"/>.
    /// </summary>
    public int? Bitrate
    {
        get => _bitrate;
        set => _bitrate = Positive(value, nameof(Bitrate));
    }

    /// <summary>
    /// Frame width in pixels. Without <see cref="Height"/> the proportions are kept.
    /// </summary>
    public int? Width
    {
        get => _width;
        set => _width = Positive(value, nameof(Width));
    }

    /// <summary>
    /// Frame height in pixels. Without <see cref="Width"/> the proportions are kept.
    /// </summary>
    public int? Height
    {
        get => _height;
        set => _height = Positive(value, nameof(Height));
    }

    /// <summary>
    /// Frames per second.
    /// </summary>
    public double? FrameRate
    {
        get => _frameRate;
        set
        {
            if (value is <= 0 || value is { } v && !double.IsFinite(v))
            {
                throw new ArgumentOutOfRangeException(nameof(FrameRate), value, $"Параметр {nameof(FrameRate)} должен быть больше нуля или не задан.");
            }

            _frameRate = value;
        }
    }

    /// <summary>
    /// <c>false</c> (default): tracks are copied without re-encoding where possible; <c>true</c>: always re-encode.
    /// </summary>
    public bool Reencode { get; set; }

    /// <summary>
    /// <c>false</c> (default): a video track is copied as it is even when its pixel format is unusual (4:4:4, 10 bit).
    /// <c>true</c>: such a track is re-encoded to the common 8-bit 4:2:0 that plays everywhere; a track already
    /// in that format is still copied. Has no effect when the video is encoded anyway.
    /// </summary>
    public bool ConvertPixelFormat { get; set; }

    private static int? Positive(int? value, string name)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"Параметр {name} должен быть больше нуля или не задан.");
        }

        return value;
    }
}
