using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Пара «из какого формата в какой», которую умеет движок.
/// </summary>
/// <param name="From">Формат входа.</param>
/// <param name="To">Формат результата.</param>
public readonly record struct ConversionPair(FileFormat From, FileFormat To);
