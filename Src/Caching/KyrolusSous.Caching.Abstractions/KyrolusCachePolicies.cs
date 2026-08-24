namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Declares reusable caching rules and automatic invalidation policies for specific entities or repository operations.
/// </summary>
/// <remarks>
/// <b>Real-World Use Cases:</b>
/// <list type="bullet">
///   <item><description><b>High-Frequency Product Lookups:</b> Applying a 1-hour absolute TTL with 5-minute Jitter to prevent stampedes when millions of users view catalog items.</description></item>
///   <item><description><b>Automatic Cache Invalidation on Mutations:</b> When an administrator updates a product via <c>AddAsync</c> or <c>UpdateAsync</c>, setting <c>ExtraInvalidationKeyPatterns = ["catalog:products:*"]</c> ensures that all cached search and listing queries are purged automatically without manual repository code.</description></item>
/// </list>
/// </remarks>
/// <param name="AbsoluteExpirationRelativeToNow">Absolute time-to-live from the moment of storage.</param>
/// <param name="SlidingExpiration">Sliding window TTL renewed upon each access.</param>
/// <param name="Jitter">Random variance added to TTL to prevent simultaneous expiration stampedes.</param>
/// <param name="NegativeCacheTtl">Short TTL for negative/null results to protect database against penetration attacks.</param>
/// <param name="Enabled">Explicit flag to enable or disable caching for the target entity/operation.</param>
/// <param name="KeySuffix">Optional key suffix appended to generated cache keys.</param>
/// <param name="ExtraInvalidationKeys">Explicit list of related cache keys to automatically invalidate when this policy executes a mutation.</param>
/// <param name="ExtraInvalidationKeyPatterns">Glob wildcard patterns to automatically invalidate upon mutation (e.g. <c>"catalog:*"</c>).</param>
public sealed record KyrolusCachePolicy(
    TimeSpan? AbsoluteExpirationRelativeToNow = null,
    TimeSpan? SlidingExpiration = null,
    TimeSpan? Jitter = null,
    TimeSpan? NegativeCacheTtl = null,
    bool? Enabled = null,
    string? KeySuffix = null,
    IReadOnlyCollection<string>? ExtraInvalidationKeys = null,
    IReadOnlyCollection<string>? ExtraInvalidationKeyPatterns = null);

/// <summary>
/// Defines a provider contract for resolving the appropriate <see cref="KyrolusCachePolicy"/> for a given value type and cache operation.
/// </summary>
public interface IKyrolusCachePolicyProvider
{
    /// <summary>
    /// Resolves the cache policy applicable to the specified object type and operation.
    /// </summary>
    /// <param name="valueType">The C# type of the cached object.</param>
    /// <param name="operation">The cache operation being performed.</param>
    /// <returns>The matching <see cref="KyrolusCachePolicy"/>, or <c>null</c> if no policy is configured.</returns>
    KyrolusCachePolicy? GetPolicy(Type valueType, KyrolusCacheOperation operation);
}

/// <summary>
/// No-op policy provider that always returns null policies.
/// </summary>
public sealed class KyrolusNullCachePolicyProvider : IKyrolusCachePolicyProvider
{
    /// <summary>
    /// Gets the singleton instance of <see cref="KyrolusNullCachePolicyProvider"/>.
    /// </summary>
    public static IKyrolusCachePolicyProvider Instance { get; } = new KyrolusNullCachePolicyProvider();

    /// <inheritdoc />
    public KyrolusCachePolicy? GetPolicy(Type valueType, KyrolusCacheOperation operation) => null;
}

/// <summary>
/// Thread-safe registry providing fluent configuration and resolution of caching policies 
/// mapped by type, operation, or globally.
/// </summary>
/// <example>
/// <code>
/// var registry = new KyrolusCachePolicyRegistry()
///     .SetDefault(new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(15)))
///     .SetForType&lt;Product&gt;(KyrolusCacheOperation.Get, new KyrolusCachePolicy(
///         AbsoluteExpirationRelativeToNow: TimeSpan.FromHours(1),
///         Jitter: TimeSpan.FromMinutes(5),
///         NegativeCacheTtl: TimeSpan.FromSeconds(30)));
/// </code>
/// </example>
public sealed class KyrolusCachePolicyRegistry : IKyrolusCachePolicyProvider
{
    private readonly ConcurrentDictionary<(Type, KyrolusCacheOperation), KyrolusCachePolicy> byTypeAndOperation = new();
    private readonly ConcurrentDictionary<KyrolusCacheOperation, KyrolusCachePolicy> byOperation = new();
    private KyrolusCachePolicy? defaultPolicy;

    /// <summary>
    /// Sets the fallback default cache policy used when no specific type or operation policy is found.
    /// </summary>
    /// <param name="policy">The default policy.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusCachePolicyRegistry SetDefault(KyrolusCachePolicy policy)
    {
        defaultPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Sets a cache policy applicable to all types executing a specific operation.
    /// </summary>
    /// <param name="operation">The cache operation.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusCachePolicyRegistry SetForOperation(KyrolusCacheOperation operation, KyrolusCachePolicy policy)
    {
        byOperation[operation] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Sets a specific cache policy for a dedicated object type <typeparamref name="T"/> and operation.
    /// </summary>
    /// <typeparam name="T">The target entity or model type.</typeparam>
    /// <param name="operation">The cache operation.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusCachePolicyRegistry SetForType<T>(KyrolusCacheOperation operation, KyrolusCachePolicy policy)
    {
        byTypeAndOperation[(typeof(T), operation)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <inheritdoc />
    public KyrolusCachePolicy? GetPolicy(Type valueType, KyrolusCacheOperation operation)
    {
        if (byTypeAndOperation.TryGetValue((valueType, operation), out var policy))
        {
            return policy;
        }

        if (byOperation.TryGetValue(operation, out policy))
        {
            return policy;
        }

        return defaultPolicy;
    }
}
