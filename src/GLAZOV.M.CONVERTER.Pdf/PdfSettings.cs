namespace GLAZOV.M.CONVERTER.Pdf;

/// <summary>
/// Settings of the PDF engine. Passed to <see cref="PdfExtension"/> when it is created;
/// the extension keeps a copy, so later changes of this object do not affect it.
/// </summary>
public sealed class PdfSettings
{
    private long _maxPixels = 50_000_000;
    private long _maxInputBytes = 256L * 1024 * 1024;

    /// <summary>
    /// The largest picture, in pixels (width × height), that is accepted.
    /// Protects memory: the picture is decoded to draw it. 50 million by default.
    /// </summary>
    public long MaxPixels
    {
        get => _maxPixels;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxPixels));
            _maxPixels = value;
        }
    }

    /// <summary>
    /// The largest input file in bytes. 256 MB by default.
    /// </summary>
    public long MaxInputBytes
    {
        get => _maxInputBytes;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxInputBytes));
            _maxInputBytes = value;
        }
    }
}
