using GLAZOV.M.CONVERTER.Archives.Model;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Reading;

/// <summary>
/// Reads entries from a 7z or RAR file using SharpCompress.
/// Solid archives (all 7z files and solid RARs) are streamed sequentially via <see cref="IReader"/>;
/// non-solid RARs are enumerated entry by entry.
/// </summary>
internal sealed class SharpArchiveReader : IArchiveReader
{
    private readonly FileStream _file;
    private readonly IArchive _archive;
    private readonly IReader? _reader;
    private readonly IEnumerator<IArchiveEntry>? _entries;
    private Stream? _open;

    /// <summary>
    /// Opens the specified 7z or RAR archive file.
    /// </summary>
    /// <param name="path">Path to the archive file on disk.</param>
    /// <param name="kind">Format display name used in error messages.</param>
    /// <exception cref="InvalidDataException">The file is damaged or not a readable archive.</exception>
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
    public async Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CloseEntryAsync().ConfigureAwait(false);
            IEntry? entry = await MoveNextEntryAsync(cancellationToken);
            if (entry is null)
            {
                return null;
            }

            ValidateEntry(entry);
            return await CreateItemAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (InvalidDataException or OperationCanceledException))
        {
            throw new InvalidDataException($"Архив повреждён или оборван: {ex.Message}", ex);
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
            // Entry disposal may fail on a truncated archive after conversion has already aborted.
        }
        finally
        {
            _reader?.Dispose();
            _entries?.Dispose();
            _archive.Dispose();
            await _file.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<IEntry?> MoveNextEntryAsync(CancellationToken cancellationToken = default)
    {
        if (_reader is null)
        {
            // Get current entry from entries
            return _entries!.MoveNext() ? _entries.Current : null;
        }
        else
        {
            // Get current entry from reader
            return await _reader.MoveToNextEntryAsync(cancellationToken) ? _reader.Entry : null;
        }
    }

    private static void ValidateEntry(IEntry entry)
    {
        // SharpCompress marks empty entries in 7z as encrypted when the stream header is encrypted; empty files carry no data.
        if (entry.IsEncrypted && !entry.IsDirectory && entry.Size > 0)
        {
            throw new InvalidDataException($"Запись «{entry.Key}» зашифрована: зашифрованные архивы не поддерживаются.");
        }

        if (!string.IsNullOrEmpty(entry.LinkTarget))
        {
            throw new InvalidDataException($"Запись «{entry.Key}» — ссылка: ссылки в архивах не переносятся, чтобы не потерять их и не выйти за пределы папки.");
        }
    }

    private async Task<ArchiveItem> CreateItemAsync(IEntry entry, CancellationToken cancellationToken)
    {
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

    private async Task CloseEntryAsync()
    {
        if (_open is not null)
        {
            Stream open = _open;
            _open = null;
            await open.DisposeAsync().ConfigureAwait(false);
        }
    }
}
