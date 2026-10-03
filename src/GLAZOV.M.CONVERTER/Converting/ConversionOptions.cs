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
}
