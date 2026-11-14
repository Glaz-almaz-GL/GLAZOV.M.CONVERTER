namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Represents a content transformation applied during conversion (such as rotation, scaling, or pitch shifting).
/// Concrete operation types are defined by format packages and conversion engines.
/// </summary>
public interface IConversionOperation
{
    /// <summary>
    /// Human-readable operation identifier used in error messages when an engine does not support it.
    /// </summary>
    string Name { get; }
}