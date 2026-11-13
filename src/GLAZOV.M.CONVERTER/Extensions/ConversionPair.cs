using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Пара «из какого формата в какой», которую умеет движок.
/// </summary>
/// <param name="From">Формат входа.</param>
/// <param name="To">Формат результата.</param>
public readonly record struct ConversionPair(FileFormat From, FileFormat To)
{
    /// <summary>
    /// <c>true</c> when one input gives several files in a target folder (a spreadsheet book written as one file per sheet).
    /// The engine then gets <see cref="ConversionRequest.OutputFolder"/> for a folder target. The flag does not make the pair another pair:
    /// equality and lookup go by <see cref="From"/> and <see cref="To"/> only.
    /// </summary>
    public bool WritesSeveralFiles { get; init; }

    /// <inheritdoc />
    public bool Equals(ConversionPair other)
    {
        return From.Equals(other.From) && To.Equals(other.To);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(From, To);
    }
}
