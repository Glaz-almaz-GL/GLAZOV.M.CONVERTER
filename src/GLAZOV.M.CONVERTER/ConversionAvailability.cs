namespace GLAZOV.M.CONVERTER;

/// <summary>Можно ли выполнить конвертацию прямо сейчас.</summary>
public enum ConversionAvailability
{
    /// <summary>Готово: всё нужное найдено.</summary>
    Ready,

    /// <summary>Нужна внешняя программа, которой нет.</summary>
    ToolMissing,
}
