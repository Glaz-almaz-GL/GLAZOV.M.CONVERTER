using System.IO.Compression;
using System.Text;

namespace GLAZOV.M.CONVERTER.Archives;

/// <summary>
/// Settings for archive conversion engines. Passed to <see cref="ArchivesExtension"/> upon creation;
/// the extension retains a snapshot copy so subsequent mutations do not affect active conversions.
/// </summary>
public class ArchivesSettings
{
    /// <summary>
    /// Maximum allowed number of entries (files and directories) in an archive. Defaults to 100,000.
    /// </summary>
    public int MaxEntries
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 100_000;

    /// <summary>
    /// Maximum total unpacked size across all entries in bytes. Defaults to 10 GB.
    /// </summary>
    public long MaxTotalBytes
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 10L * 1024 * 1024 * 1024;

    /// <summary>
    /// Maximum unpacked size of a single archive entry in bytes. Defaults to 4 GB.
    /// </summary>
    public long MaxEntryBytes
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 4L * 1024 * 1024 * 1024;

    /// <summary>
    /// Maximum allowed ratio of total unpacked bytes to input archive size. Checked once more than 1 MB has been unpacked. Defaults to 500.
    /// </summary>
    public int MaxCompressionRatio
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 500;

    /// <summary>
    /// Compression level applied when writing compressed output archives (ZIP, TAR.GZ, GZip). Defaults to <see cref="CompressionLevel.Optimal"/>.
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// Code page used to decode entry names in ZIP archives that lack the UTF-8 flag. Defaults to 437 (IBM437).
    /// </summary>
    public int ZipNameCodePage
    {
        get;
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

            field = value;
        }
    } = 437;

    /// <summary>
    /// Creates a shallow snapshot copy of the current settings.
    /// </summary>
    protected internal virtual ArchivesSettings Copy()
    {
        return (ArchivesSettings)MemberwiseClone();
    }
}
