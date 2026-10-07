namespace GLAZOV.M.CONVERTER.Archives.Tests;

/// <summary>
/// An entry as an independent reader (the standard library) sees it.
/// </summary>
/// <param name="Name">Name of the entry.</param>
/// <param name="IsDirectory">Whether it is a folder.</param>
/// <param name="Data">Content; empty for a folder.</param>
/// <param name="Modified">Time of the last change.</param>
internal sealed record ArchiveSnapshot(string Name, bool IsDirectory, byte[] Data, DateTimeOffset Modified);
