using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;

namespace GLAZOV.M.CONVERTER.Capabilities;

/// <summary>
/// Одна поддерживаемая конвертация: из какого типа в какой формат и можно ли её выполнить сейчас.
/// </summary>
public sealed class Conversion
{
    internal Conversion(Format from, Format to, ConversionAvailability availability)
    {
        From = from;
        To = to;
        Availability = availability;
    }

    /// <summary>
    /// Тип входа.
    /// </summary>
    public Format From { get; }

    /// <summary>
    /// Формат результата.
    /// </summary>
    public Format To { get; }

    /// <summary>
    /// Готова ли конвертация или нужна программа.
    /// </summary>
    public ConversionAvailability Availability { get; }
}
