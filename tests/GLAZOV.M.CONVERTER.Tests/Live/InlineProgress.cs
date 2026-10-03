namespace GLAZOV.M.CONVERTER.Tests.Live;

/// <summary>
/// Приёмник хода, который сразу вызывает переданное действие (без пересылки в другой поток, как у <see cref="Progress{T}"/>).
/// </summary>
public sealed class InlineProgress<T>(Action<T> action) : IProgress<T>
{
    public void Report(T value)
    {
        action(value);
    }
}
