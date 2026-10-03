namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Действие над изображением, выполняемое в том же вызове конвертации (следующие версии).
/// </summary>
public sealed class Operation
{
    private Operation(string name, int? percent)
    {
        Name = name;
        Percent = percent;
    }

    internal string Name { get; }

    internal int? Percent { get; }

    /// <summary>
    /// Повернуть вправо на 90 градусов.
    /// </summary>
    public static Operation RotateRight { get; } = new("RotateRight", null);

    /// <summary>
    /// Изменить размер до заданного процента исходного.
    /// </summary>
    /// <param name="percent">Процент, больше нуля.</param>
    public static Operation ScalePercent(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(percent);
        return new Operation("ScalePercent", percent);
    }
}
