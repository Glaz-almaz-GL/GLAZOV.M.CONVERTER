namespace GLAZOV.M.CONVERTER;

/// <summary>
/// Ход одной конвертации.
/// </summary>
public sealed class ConversionProgress
{
    internal ConversionProgress(double percent) => Percent = percent;

    /// <summary>
    /// Выполнено, процентов (0–100).
    /// </summary>
    public double Percent { get; }
}
