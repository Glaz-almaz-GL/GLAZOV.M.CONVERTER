namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Действие над изображением, выполняемое в том же вызове конвертации.
/// Действия выполняются по порядку их добавления в <see cref="ConversionOptions.Operations"/>.
/// </summary>
public sealed class Operation
{
    private Operation(OperationKind kind, int? percent)
    {
        Kind = kind;
        Percent = percent;
    }

    /// <summary>
    /// Вид действия.
    /// </summary>
    public OperationKind Kind { get; }

    /// <summary>
    /// Процент для <see cref="OperationKind.ScalePercent"/>, больше нуля;
    /// для остальных видов — <c>null</c>.
    /// </summary>
    public int? Percent { get; }

    /// <summary>
    /// Повернуть вправо на 90 градусов.
    /// </summary>
    public static Operation RotateRight { get; } = new(OperationKind.RotateRight, null);

    /// <summary>
    /// Изменить размер до заданного процента исходного.
    /// </summary>
    /// <param name="percent">Процент, больше нуля.</param>
    public static Operation ScalePercent(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(percent);
        return new Operation(OperationKind.ScalePercent, percent);
    }
}
