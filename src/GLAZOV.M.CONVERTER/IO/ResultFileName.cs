using System.Collections.Frozen;

namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// Rule for the file name of a result an engine writes into the output folder.
/// Safe for Windows, Linux, and macOS.
/// </summary>
internal static class ResultFileName
{
    private const int MaxLength = 255;

    private static readonly FrozenSet<string> ReservedStems = FrozenSet.ToFrozenSet(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when the name cannot be a plain file name on every system.
    /// </summary>
    /// <param name="fileName">The proposed name, without a folder.</param>
    internal static void Validate(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (fileName.Length > MaxLength)
        {
            throw Refuse($"имя длиннее {MaxLength} знаков", nameof(fileName));
        }

        if (fileName.Contains("..", StringComparison.Ordinal))
        {
            throw Refuse("в имени содержится последовательность \"..\"", nameof(fileName));
        }

        if (fileName[^1] is '.' or ' ')
        {
            throw Refuse("имя кончается точкой или пробелом", nameof(fileName));
        }

        // 0x20 is space, 0x7F is DEL; the rest are Windows forbidden characters. The check is done on the whole name, not just the stem,
        // because a stem that ends with a space or dot is already refused above.
        if (fileName.Any(c => c is < (char)0x20 or (char)0x7F or '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'))
        {
            throw Refuse("в имени присутствует недопустимый символ или разделитель папок", nameof(fileName));
        }

        int firstDot = fileName.IndexOf('.', StringComparison.Ordinal);

        ReadOnlySpan<char> stemSpan = firstDot < 0
            ? fileName.AsSpan()
            : fileName.AsSpan(0, firstDot);

        stemSpan = stemSpan.TrimEnd(' ');

        FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> lookup = ReservedStems.GetAlternateLookup<ReadOnlySpan<char>>();

        if (lookup.Contains(stemSpan))
        {
            throw Refuse("имя совпадает с зарезервированным именем устройства Windows", nameof(fileName));
        }
    }

    private static ArgumentException Refuse(string reason, string paramName)
    {
        return new ArgumentException($"Имя файла результата недопустимо: {reason}.", paramName);
    }
}