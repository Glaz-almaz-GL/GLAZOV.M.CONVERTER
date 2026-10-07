namespace GLAZOV.M.CONVERTER.ArchivesExtra.Safety;

/// <summary>
/// Watches the limits of one conversion: the number of entries, the size of an entry, the total size and the ratio of the unpacked size
/// to the size of the input. Every check throws <see cref="InvalidDataException"/> with the name of the limit.
/// </summary>
internal sealed class ArchiveGuard
{
    private const long RatioThreshold = 1024 * 1024;

    private readonly ArchivesLimits _limits;
    private int _entries;
    private long _entryBytes;
    private long _totalBytes;

    /// <summary>
    /// Creates the guard.
    /// </summary>
    /// <param name="limits">Copy of the limits of the settings.</param>
    /// <param name="inputBytes">Size of the input archive for the ratio, or <c>null</c> when it is not known (the consumed size is asked then).</param>
    /// <param name="consumed">How many bytes of the input have been read so far.</param>
    internal ArchiveGuard(ArchivesLimits limits, long? inputBytes, Func<long> consumed)
    {
        _limits = limits;
        InputBytes = inputBytes;
        Consumed = consumed;
    }

    internal long? InputBytes { get; }

    internal Func<long> Consumed { get; }

    /// <summary>
    /// Counts a new entry and checks its declared size.
    /// </summary>
    /// <param name="name">Name of the entry, for the message.</param>
    /// <param name="declaredLength">Size the archive declares, if it does.</param>
    internal void BeginEntry(string name, long? declaredLength)
    {
        _entries++;
        _entryBytes = 0;
        if (_entries > _limits.MaxEntries)
        {
            throw new InvalidDataException($"Архив больше предела: записей больше {_limits.MaxEntries} (настройка MaxEntries).");
        }

        if (declaredLength > _limits.MaxEntryBytes)
        {
            throw new InvalidDataException($"Запись «{name}» больше предела: {declaredLength} байт при пределе {_limits.MaxEntryBytes} (настройка MaxEntryBytes).");
        }
    }

    /// <summary>
    /// Counts unpacked bytes of the current entry.
    /// </summary>
    /// <param name="name">Name of the entry, for the message.</param>
    /// <param name="count">How many bytes were just unpacked.</param>
    internal void Unpacked(string name, int count)
    {
        _entryBytes += count;
        _totalBytes += count;
        if (_entryBytes > _limits.MaxEntryBytes)
        {
            throw new InvalidDataException($"Запись «{name}» больше предела: больше {_limits.MaxEntryBytes} байт (настройка MaxEntryBytes).");
        }

        if (_totalBytes > _limits.MaxTotalBytes)
        {
            throw new InvalidDataException($"Архив больше предела: распакованные записи больше {_limits.MaxTotalBytes} байт (настройка MaxTotalBytes).");
        }

        long input = InputBytes ?? Consumed();
        if (_totalBytes > RatioThreshold && _totalBytes > input * (long)_limits.MaxCompressionRatio)
        {
            throw new InvalidDataException(
                $"Похоже на архив-бомбу: распаковано {_totalBytes} байт из {Math.Max(input, 1)} при пределе отношения {_limits.MaxCompressionRatio} (настройка MaxCompressionRatio).");
        }
    }
}
