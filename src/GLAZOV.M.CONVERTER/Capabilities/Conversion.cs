using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Capabilities;

/// <summary>
/// Одна поддерживаемая конвертация: из какого типа в какой формат и можно ли её выполнить сейчас.
/// </summary>
public sealed record Conversion
{
    internal Conversion(FileFormat from, FileFormat to, ConversionAvailability availability)
    {
        From = from;
        To = to;
        Availability = availability;
    }

    /// <summary>
    /// Тип входа.
    /// </summary>
    public FileFormat From { get; init; }

    /// <summary>
    /// Формат результата.
    /// </summary>
    public FileFormat To { get; init; }

    /// <summary>
    /// Готова ли конвертация или нужна программа.
    /// </summary>
    public ConversionAvailability Availability { get; init; }
}
