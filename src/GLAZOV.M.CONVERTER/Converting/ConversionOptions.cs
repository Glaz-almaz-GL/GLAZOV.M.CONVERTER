namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Дополнительные параметры одного вызова конвертации.
/// </summary>
public sealed class ConversionOptions
{
    /// <summary>
    /// Действия над изображением, выполняемые вместе с конвертацией.
    /// </summary>
    public ICollection<Operation> Operations { get; } = [];

    /// <summary>
    /// Параметры качества звука. Не задано — всё берётся из источника или умолчаний движка.
    /// </summary>
    public AudioOptions? Audio { get; set; }
}
