namespace GLAZOV.M.CONVERTER.Archives.Model;

/// <summary>
/// What an entry of an archive says about itself, in the form every kind of archive can read and write.
/// </summary>
/// <param name="Name">Name with "/" as the separator; a folder ends with "/".</param>
/// <param name="IsDirectory">Whether the entry is a folder.</param>
/// <param name="Modified">Time of the last change.</param>
/// <param name="Mode">Unix permissions where the archive carries them, otherwise <c>null</c>.</param>
/// <param name="Length">Size of the content when unpacked, if the archive says so.</param>
public sealed record ArchiveEntryInfo(string Name, bool IsDirectory, DateTimeOffset Modified, UnixFileMode? Mode, long? Length);
