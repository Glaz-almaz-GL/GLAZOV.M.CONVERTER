namespace GLAZOV.M.CONVERTER.Formats.Options;

/// <summary>
/// Параметры изображения для одного вызова конвертации. Необязательные:
/// не задано — берётся умолчание движка для выбранного формата.
/// </summary>
public sealed class ImageOptions
{
    /// <summary>
    /// Качество сжатия от 1 (наименьшее) до 100 (наибольшее). Действует для форматов с потерями (JPEG, WebP).
    /// Не задано — умолчание движка. Значение вне диапазона отвергается сразу.
    /// </summary>
    public int? Quality
    {
        get;
        set
        {
            if (value is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(Quality), value, "Качество изображения — число от 1 до 100 или не задано.");
            }

            field = value;
        }
    }
}
