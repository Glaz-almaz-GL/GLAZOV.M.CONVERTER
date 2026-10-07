namespace GLAZOV.M.CONVERTER.Archives.Model;

/// <summary>
/// An entry read from an archive: its description and its content (<c>null</c> for a folder). The content is valid until the next entry is read.
/// </summary>
/// <param name="Info">Description.</param>
/// <param name="Content">Content stream.</param>
/// <param name="CompressedLength">Size of the entry inside the archive, if the archive says so; it feeds the progress.</param>
internal sealed record ArchiveItem(ArchiveEntryInfo Info, Stream? Content, long? CompressedLength = null);
