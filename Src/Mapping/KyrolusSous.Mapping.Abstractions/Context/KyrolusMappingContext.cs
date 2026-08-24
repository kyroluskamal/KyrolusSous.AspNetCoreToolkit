namespace KyrolusSous.Mapping.Abstractions.Context;

/// <summary>
/// Encapsulates execution state, parameters, and circular reference tracking during object-to-object mapping operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is Circular Reference Tracking?</b>
/// When mapping bi-directional object graphs (e.g. <c>Parent.Children</c> and each <c>Child.Parent</c>), a naive mapper
/// would enter an infinite recursion loop resulting in a <see cref="StackOverflowException"/>.
/// <see cref="KyrolusMappingContext"/> tracks already-visited source instances by reference, returning the previously mapped
/// destination instance immediately.
/// </para>
/// <para>
/// <b>Real-World Use Case:</b>
/// Mapping deep domain aggregate trees (e.g. <c>Order -> OrderItems -> Order</c>) safely and passing runtime parameters
/// (e.g. current user timezone, culture, or tenant context) into mapping expressions and custom resolvers.
/// </para>
/// </remarks>
public sealed class KyrolusMappingContext
{
    private readonly struct ReferenceKey(object source, Type targetType) : IEquatable<ReferenceKey>
    {
        public object Source { get; } = source;
        public Type TargetType { get; } = targetType;

        public bool Equals(ReferenceKey other) =>
            ReferenceEquals(Source, other.Source) && TargetType == other.TargetType;

        public override bool Equals(object? obj) => obj is ReferenceKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Source);
                return (hash * 397) ^ TargetType.GetHashCode();
            }
        }
    }

    private readonly Dictionary<ReferenceKey, object> _referenceMap = [];
    private Dictionary<string, object?>? _items;

    /// <summary>
    /// Gets a dictionary of custom parameters and state passed to resolvers and converters during mapping.
    /// </summary>
    public IDictionary<string, object?> Items => _items ??= new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Attempts to retrieve an already-mapped destination instance for the given <paramref name="source"/> object and <paramref name="targetType"/>.
    /// </summary>
    /// <param name="source">The source object instance.</param>
    /// <param name="targetType">The destination type.</param>
    /// <param name="target">When this method returns, contains the already-mapped destination object if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a previously mapped instance was found; otherwise, <c>false</c>.</returns>
    public bool TryGetMapped(object source, Type targetType, out object? target)
    {
        if (source is null || targetType is null)
        {
            target = null;
            return false;
        }

        var key = new ReferenceKey(source, targetType);
        return _referenceMap.TryGetValue(key, out target);
    }

    /// <summary>
    /// Attempts to retrieve an already-mapped destination instance for the given <paramref name="source"/> object
    /// to preserve object identity and prevent infinite circular recursion loops.
    /// </summary>
    /// <typeparam name="TTarget">The target destination type.</typeparam>
    /// <param name="source">The source object instance.</param>
    /// <param name="target">When this method returns, contains the already-mapped destination object if found; otherwise, <c>default</c>.</param>
    /// <returns><c>true</c> if a previously mapped instance was found; otherwise, <c>false</c>.</returns>
    public bool TryGetMapped<TTarget>(object source, out TTarget? target)
    {
        if (TryGetMapped(source, typeof(TTarget), out var obj) && obj is TTarget typed)
        {
            target = typed;
            return true;
        }

        target = default;
        return false;
    }

    /// <summary>
    /// Registers a newly created destination instance for the given source object in the circular reference cache.
    /// </summary>
    /// <param name="source">The source object instance.</param>
    /// <param name="target">The newly constructed destination target object.</param>
    public void RegisterMapped(object source, object target)
    {
        if (source is null || target is null)
        {
            return;
        }

        var key = new ReferenceKey(source, target.GetType());
        _referenceMap[key] = target;
    }

    /// <summary>
    /// Gets a typed parameter value from <see cref="Items"/>, or returns <paramref name="defaultValue"/> if not found.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The item parameter key.</param>
    /// <param name="defaultValue">The fallback default value if the key does not exist.</param>
    /// <returns>The typed parameter value.</returns>
    public T? GetItem<T>(string key, T? defaultValue = default)
    {
        if (_items is not null && _items.TryGetValue(key, out var val) && val is T typed)
        {
            return typed;
        }

        return defaultValue;
    }

    /// <summary>
    /// Sets a typed parameter value in <see cref="Items"/>.
    /// </summary>
    /// <param name="key">The item parameter key.</param>
    /// <param name="value">The value to associate with the key.</param>
    public KyrolusMappingContext SetItem(string key, object? value)
    {
        Items[key] = value;
        return this;
    }

    /// <summary>
    /// Clears all circular reference tracking and custom items.
    /// </summary>
    public void Reset()
    {
        _referenceMap.Clear();
        _items?.Clear();
    }
}
