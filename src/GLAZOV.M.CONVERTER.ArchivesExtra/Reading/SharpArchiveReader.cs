using GLAZOV.M.CONVERTER.ArchivesExtra.Model;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Reading;

/// <summary>
/// Reads the entries of a 7z or RAR file with SharpCompress. A solid archive (every 7z, a solid RAR) is read in the order its entries lie in the file,
/// the only way to read it without unpacking its blocks again and again; a RAR that is not solid is read entry by entry. The file must be readable
/// at any place, so the engine gives it as a file.
/// </summary>
internal sealed class SharpArchiveReader : IArchiveReader
{
    private readonly FileStream _file;
    private readonly IArchive _archive;
    private readonly IReader? _reader;
    private readonly IEnumerator<IArchiveEntry>? _entries;
    private Stream? _open;

    /// <summary>
    /// Opens the archive.
    /// </summary>
    /// <param name="path">The 7z or RAR file.</param>
    /// <param name="kind">Name of the kind, for the messages.</param>
    /// <exception cref="InvalidDataException">The file is not a readable archive of that kind.</exception>
    internal SharpArchiveReader(string path, string kind)
    {
        _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
        try
        {
            _archive = ArchiveFactory.Open(_file, new ReaderOptions { LeaveStreamOpen = true });
            if (_archive.IsSolid || _archive.Type == ArchiveType.SevenZip)
            {
                _reader = _archive.ExtractAllEntries();
            }
            else
            {
                _entries = _archive.Entries.GetEnumerator();
            }
        }
        catch (Exception ex) when (ex is not (InvalidDataException or OperationCanceledException))
        {
            _file.Dispose();
            throw new InvalidDataException($"Файл {kind} повреждён или не читается: {ex.Message}", ex);
        }
        catch
        {
            _file.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CloseEntryAsync().ConfigureAwait(false);
            while (_reader is not null ? _reader.MoveToNextEntry() : _entries!.MoveNext())
            {
                IEntry entry = _reader is not null ? _reader.Entry : _entries!.Current;
                // SharpCompress marks an empty file of a 7z as encrypted when the archive says so for its stream; an entry without content hides nothing.
                if (entry.IsEncrypted && !entry.IsDirectory && entry.Size > 0)
                {
                    throw new InvalidDataException($"Запись «{entry.Key}» зашифрована: зашифрованные архивы не поддерживаются.");
                }

                if (!string.IsNullOrEmpty(entry.LinkTarget))
                {
                    throw new InvalidDataException($"Запись «{entry.Key}» — ссылка: ссылки в архивах не переносятся, чтобы не потерять их и не выйти за пределы папки.");
                }

                DateTimeOffset modified = entry.LastModifiedTime is { } time ? new DateTimeOffset(time) : DateTimeOffset.UtcNow;
                ArchiveEntryInfo info = new(entry.Key ?? string.Empty, entry.IsDirectory, modified, null, entry.IsDirectory ? null : entry.Size);
                if (entry.IsDirectory)
                {
                    return new ArchiveItem(info, null, entry.CompressedSize);
                }

                _open = _reader is not null
                    ? await _reader.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false)
                    : await _entries!.Current.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
                return new ArchiveItem(info, _open, entry.CompressedSize);
            }

            return null;
        }
        catch (Exception ex) when (ex is not (InvalidDataException or OperationCanceledException))
        {
            throw new InvalidDataException($"Архив повреждён или оборван: {ex.Message}", ex);
        }
    }

    // Closing an entry reads what was left of it; on a damaged archive that fails, and the failure is the archive's, not the engine's.
    private async Task CloseEntryAsync()
    {
        if (_open is not null)
        {
            Stream open = _open;
            _open = null;
            await open.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await CloseEntryAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            // the conversion has already failed or is over; the entry is dropped
        }
        finally
        {
            _reader?.Dispose();
            _entries?.Dispose();
            _archive.Dispose();
            await _file.DisposeAsync().ConfigureAwait(false);
        }
    }
}
