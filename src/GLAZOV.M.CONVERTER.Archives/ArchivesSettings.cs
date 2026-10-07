using System.IO.Compression;
using System.Text;

namespace GLAZOV.M.CONVERTER.Archives;

/// <summary>
/// Settings of the archive engine. Passed to <see cref="ArchivesExtension"/> when it is created;
/// the extension keeps a copy, so later changes of this object do not affect it.
/// </summary>
/// <remarks>
/// The limits protect against archive bombs: an archive that goes over any of them is refused with an error that names the limit,
/// never cut short.
/// </remarks>
public sealed class ArchivesSettings
{
    private int _maxEntries = 100_000;
    private long _maxTotalBytes = 10L * 1024 * 1024 * 1024;
    private long _maxEntryBytes = 4L * 1024 * 1024 * 1024;
    private int _maxCompressionRatio = 500;
    private int _zipNameCodePage = 437;

    /// <summary>
    /// The largest number of entries (files and folders) in an archive. 100 000 by default.
    /// </summary>
    public int MaxEntries
    {
        get => _maxEntries;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxEntries));
            _maxEntries = value;
        }
    }

    /// <summary>
    /// The largest total size of all entries when unpacked, in bytes. 10 GB by default.
    /// </summary>
    public long MaxTotalBytes
    {
        get => _maxTotalBytes;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxTotalBytes));
            _maxTotalBytes = value;
        }
    }

    /// <summary>
    /// The largest size of one entry when unpacked, in bytes. 4 GB by default.
    /// </summary>
    public long MaxEntryBytes
    {
        get => _maxEntryBytes;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxEntryBytes));
            _maxEntryBytes = value;
        }
    }

    /// <summary>
    /// The largest ratio of the unpacked size to the size of the input archive. It is checked once more than a megabyte has been unpacked,
    /// so a small honest archive of zeroes is not taken for a bomb. 500 by default.
    /// </summary>
    public int MaxCompressionRatio
    {
        get => _maxCompressionRatio;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxCompressionRatio));
            _maxCompressionRatio = value;
        }
    }

    /// <summary>
    /// How hard the result (ZIP, TAR.GZ, GZip) is compressed. <see cref="CompressionLevel.Optimal"/> by default.
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// The code page of the names in a ZIP input whose entries are not marked as UTF-8. 437 (IBM437), as the format says, by default;
    /// old archives made in Russian are usually 866 (IBM866). Names of a ZIP result are always UTF-8.
    /// </summary>
    public int ZipNameCodePage
    {
        get => _zipNameCodePage;
        set
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _ = Encoding.GetEncoding(value);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                throw new ArgumentOutOfRangeException(nameof(ZipNameCodePage), value, "Кодовая страница не поддерживается.");
            }

            _zipNameCodePage = value;
        }
    }

    internal ArchivesSettings Copy()
    {
        return (ArchivesSettings)MemberwiseClone();
    }
}
