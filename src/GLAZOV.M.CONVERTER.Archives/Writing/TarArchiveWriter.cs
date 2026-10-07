using System.Formats.Tar;
using GLAZOV.M.CONVERTER.Archives.Model;

namespace GLAZOV.M.CONVERTER.Archives.Writing;

/// <summary>
/// Writes a TAR archive (POSIX pax: long names and any characters are fine) to a stream, entry by entry.
/// </summary>
internal sealed class TarArchiveWriter : IArchiveWriter
{
    private readonly TarWriter _tar;
    private readonly Stream _output;
    private readonly string _tempFolder;
    private int _entries;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="output">Where the archive goes; it stays open.</param>
    /// <param name="tempFolder">Folder for the entries whose size is not known before they are read.</param>
    internal TarArchiveWriter(Stream output, string tempFolder)
    {
        _tar = new TarWriter(output, TarEntryFormat.Pax, leaveOpen: true);
        _output = output;
        _tempFolder = tempFolder;
    }

    /// <inheritdoc />
    public async Task WriteAsync(ArchiveEntryInfo info, Stream? content, CancellationToken cancellationToken)
    {
        _entries++;
        if (content is null)
        {
            PaxTarEntry folder = new(TarEntryType.Directory, info.Name) { ModificationTime = info.Modified, Mode = info.Mode ?? (UnixFileMode)0b111_101_101 };
            await _tar.WriteEntryAsync(folder, cancellationToken).ConfigureAwait(false);
            return;
        }

        // TAR writes the size before the content: a stream whose size is not known goes through a temporary file first.
        string spool = Path.Combine(_tempFolder, $"archive-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream file = new(spool, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose))
            {
                await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                file.Position = 0;
                PaxTarEntry entry = new(TarEntryType.RegularFile, info.Name)
                {
                    ModificationTime = info.Modified,
                    Mode = info.Mode ?? (UnixFileMode)0b110_100_100,
                    DataStream = file,
                };
                await _tar.WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (File.Exists(spool))
            {
                File.Delete(spool);
            }
        }
    }

    /// <inheritdoc />
    public void Complete()
    {
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _tar.DisposeAsync().ConfigureAwait(false);
        if (_entries == 0)
        {
            // TarWriter writes nothing when it has no entries; an empty TAR archive is two blocks of zeroes.
            await _output.WriteAsync(new byte[1024]).ConfigureAwait(false);
        }
    }
}
