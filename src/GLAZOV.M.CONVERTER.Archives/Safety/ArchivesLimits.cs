namespace GLAZOV.M.CONVERTER.Archives.Safety;

/// <summary>
/// The limits of <see cref="ArchivesSettings"/> as the engine reads them.
/// </summary>
/// <param name="MaxEntries">Most entries.</param>
/// <param name="MaxTotalBytes">Most unpacked bytes in all.</param>
/// <param name="MaxEntryBytes">Most unpacked bytes in one entry.</param>
/// <param name="MaxCompressionRatio">Largest ratio of unpacked to input size.</param>
internal sealed record ArchivesLimits(int MaxEntries, long MaxTotalBytes, long MaxEntryBytes, int MaxCompressionRatio)
{
    /// <summary>
    /// Takes the limits of the settings.
    /// </summary>
    /// <param name="settings">The (copied) settings.</param>
    internal static ArchivesLimits From(ArchivesSettings settings)
    {
        return new ArchivesLimits(settings.MaxEntries, settings.MaxTotalBytes, settings.MaxEntryBytes, settings.MaxCompressionRatio);
    }
}
