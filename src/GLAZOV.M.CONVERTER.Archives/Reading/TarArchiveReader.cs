using GLAZOV.M.CONVERTER.Archives.Model;
using System.Formats.Tar;

namespace GLAZOV.M.CONVERTER.Archives.Reading;

/// <summary>
/// Reads the entries of a TAR stream (plain or already unpacked from GZip). Links, devices and pipes are refused: they cannot be moved
/// into another kind of archive without being lost or turned into something else.
/// </summary>
/// <remarks>
/// Creates the reader.
/// </remarks>
/// <param name="input">The TAR stream; it is read once, forward.</param>
public sealed class TarArchiveReader(Stream input) : IArchiveReader
{
    private readonly TarReader _tar = new(input, leaveOpen: true);

    /// <inheritdoc />
    public async Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            TarEntry? entry;
            try
            {
                entry = await _tar.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or FormatException)
            {
                throw new InvalidDataException("Файл TAR повреждён или оборван: " + ex.Message, ex);
            }

            if (entry is null)
            {
                return null;
            }

            switch (entry.EntryType)
            {
                case TarEntryType.GlobalExtendedAttributes:
                    continue;

                case TarEntryType.Directory:
                    return new ArchiveItem(Describe(entry, true), null);

                case TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile:
                    return new ArchiveItem(Describe(entry, false), entry.DataStream ?? Stream.Null);

                case TarEntryType.SymbolicLink or TarEntryType.HardLink:
                    throw new InvalidDataException($"Запись «{entry.Name}» — ссылка: ссылки в архивах не переносятся, чтобы не потерять их и не выйти за пределы папки.");

                default:
                    throw new InvalidDataException($"Запись «{entry.Name}» — особый файл ({entry.EntryType}): такие записи не переносятся.");
            }
        }
    }

    private static ArchiveEntryInfo Describe(TarEntry entry, bool directory)
    {
        return new ArchiveEntryInfo(entry.Name, directory, entry.ModificationTime, entry.Mode, directory ? null : entry.Length);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _tar.DisposeAsync();
    }
}
