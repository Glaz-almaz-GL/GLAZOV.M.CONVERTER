using GLAZOV.M.CONVERTER.Converting;

namespace GLAZOV.M.CONVERTER.Execution;

/// <summary>
/// Passes engine progress to the subscriber of one conversion: values are clamped, only growing ones go through,
/// and 100 is never taken from an engine (the library reports it after the result is written). Thread-safe.
/// </summary>
internal sealed class ProgressGate
{
    private readonly IProgress<ConversionProgress> _target;
    private readonly object _lock = new();
    private int _last;

    internal ProgressGate(IProgress<ConversionProgress> target)
    {
        _target = target;
    }

    /// <summary>
    /// Reports a percent from the engine if it moves the progress forward.
    /// </summary>
    /// <param name="percent">Percent done; out-of-range values are clamped.</param>
    internal void Report(int percent)
    {
        int value = Math.Clamp(percent, 0, 100);
        lock (_lock)
        {
            if (value <= _last || value >= 100)
            {
                return;
            }

            _last = value;
            _target.Report(new ConversionProgress(value));
        }
    }
}
