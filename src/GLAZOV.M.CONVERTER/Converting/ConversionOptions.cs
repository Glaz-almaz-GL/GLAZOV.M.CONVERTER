using System.Diagnostics.CodeAnalysis;

namespace GLAZOV.M.CONVERTER.Converting;

/// <summary>
/// Extensible container for per-call conversion options and content transformations.
/// Engines and format packages attach their own option types without modifying the core library.
/// </summary>
public sealed class ConversionOptions
{
    private readonly Dictionary<Type, object> _entries = [];

    /// <summary>
    /// Gets the ordered collection of transformations to apply during conversion.
    /// Requires an engine with <see cref="Extensions.IConversionEngine.SupportsOperations"/> set to <c>true</c>.
    /// </summary>
    public IList<IConversionOperation> Operations { get; } = [];

    /// <summary>
    /// Stores an option object of type <typeparamref name="T"/>, replacing any previous instance of the same type.
    /// </summary>
    /// <typeparam name="T">Option type.</typeparam>
    /// <param name="value">Option instance.</param>
    public ConversionOptions Set<T>(T value) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        _entries[typeof(T)] = value;
        return this;
    }

    /// <summary>
    /// Retrieves a stored option object of type <typeparamref name="T"/>, or <c>null</c> if not set.
    /// </summary>
    /// <typeparam name="T">Option type.</typeparam>
    public T? Get<T>() where T : class
    {
        return _entries.TryGetValue(typeof(T), out object? value) ? (T)value : null;
    }

    /// <summary>
    /// Attempts to retrieve a stored option object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Option type.</typeparam>
    /// <param name="value">Retrieved option instance when found.</param>
    public bool TryGet<T>([NotNullWhen(true)] out T? value) where T : notnull
    {
        if (_entries.TryGetValue(typeof(T), out object? raw))
        {
            value = (T)raw;
            return true;
        }

        value = default;
        return false;
    }
}
