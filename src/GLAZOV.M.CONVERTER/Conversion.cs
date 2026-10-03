namespace GLAZOV.M.CONVERTER;

/// <summary>Одна поддерживаемая конвертация: из какого типа в какой формат и можно ли её выполнить сейчас.</summary>
public sealed class Conversion
{
    internal Conversion(FileType from, Format to, ConversionAvailability availability)
    {
        From = from;
        To = to;
        Availability = availability;
    }

    /// <summary>Тип входа.</summary>
    public FileType From { get; }

    /// <summary>Формат результата.</summary>
    public Format To { get; }

    /// <summary>Готова ли конвертация или нужна программа.</summary>
    public ConversionAvailability Availability { get; }
}
