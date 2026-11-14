using GLAZOV.M.CONVERTER.Archives;

namespace GLAZOV.M.CONVERTER.ArchivesExtra;

/// <summary>
/// Settings for the extra archive engine (7z, RAR, XZ, BZip2, and standard archive formats).
/// Inherits all archive-bomb protection limits and ZIP code page options from <see cref="ArchivesSettings"/>.
/// </summary>
public sealed class ArchivesExtraSettings : ArchivesSettings
{
    internal new ArchivesExtraSettings Copy()
    {
        return (ArchivesExtraSettings)MemberwiseClone();
    }
}