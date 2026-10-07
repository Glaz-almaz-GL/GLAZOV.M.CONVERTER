using GLAZOV.M.CONVERTER.ArchivesExtra.Model;
using System.IO.Compression;
using System.Text;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Reading;

/// <summary>
/// Reads the entries of a ZIP file. The file must be readable at any place (the central directory lies at the end), so the engine gives it as a file.
/// </summary>
internal sealed class ZipArchiveReader : IArchiveReader
{
    private const int SymbolicLinkType = 0xA000;

    private readonly ZipArchive _zip;
    private readonly IEnumerator<ZipArchiveEntry> _entries;
    private Stream? _open;

    /// <summary>
    /// Opens the archive.
    /// </summary>
    /// <param name="path">The ZIP file.</param>
    /// <param name="nameEncoding">Encoding of the names of entries not marked as UTF-8.</param>
    /// <exception cref="InvalidDataException">The file is not a readable ZIP archive.</exception>
    internal ZipArchiveReader(string path, Encoding nameEncoding)
    {
        FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess);
        try
        {
            _zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false, nameEncoding);
            _entries = _zip.Entries.GetEnumerator();
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken)
    {
        if (_open is not null)
        {
            await _open.DisposeAsync().ConfigureAwait(false);
            _open = null;
        }

        if (!_entries.MoveNext())
        {
            return null;
        }

        ZipArchiveEntry entry = _entries.Current;
        bool directory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
        if (entry.IsEncrypted)
        {
            throw new InvalidDataException($"Запись «{entry.FullName}» зашифрована: зашифрованные архивы не поддерживаются.");
        }

        if (((entry.ExternalAttributes >> 16) & 0xF000) == SymbolicLinkType)
        {
            throw new InvalidDataException($"Запись «{entry.FullName}» — ссылка: ссылки в архивах не переносятся, чтобы не потерять их и не выйти за пределы папки.");
        }

        int bits = (entry.ExternalAttributes >> 16) & 0xFFF;
        UnixFileMode? mode = bits != 0 ? (UnixFileMode)bits : null;
        ArchiveEntryInfo info = new(entry.FullName, directory, entry.LastWriteTime, mode, directory ? null : entry.Length);
        if (directory)
        {
            return new ArchiveItem(info, null, entry.CompressedLength);
        }

        _open = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new ArchiveItem(info, _open, entry.CompressedLength);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_open is not null)
        {
            await _open.DisposeAsync().ConfigureAwait(false);
        }

        _entries.Dispose();
        await _zip.DisposeAsync().ConfigureAwait(false);
    }
}
