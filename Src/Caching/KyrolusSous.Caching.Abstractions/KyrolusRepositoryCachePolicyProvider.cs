namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Defines a provider contract for resolving caching policies dynamically during repository operations (e.g. EF Core or Marten repositories).
/// </summary>
public interface IKyrolusRepositoryCachePolicyProvider
{
    /// <summary>
    /// Asynchronously resolves the applicable <see cref="KyrolusCachePolicy"/> for a repository entity and operation.
    /// </summary>
    /// <param name="context">The contextual metadata describing the repository, entity type, and operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved cache policy, or <c>null</c> if caching is not enabled for this repository operation.</returns>
    ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusRepositoryCachePolicyContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op implementation of <see cref="IKyrolusRepositoryCachePolicyProvider"/> that returns no caching policies.
/// </summary>
public sealed class KyrolusNoopRepositoryCachePolicyProvider : IKyrolusRepositoryCachePolicyProvider
{
    /// <summary>
    /// Gets the singleton instance of <see cref="KyrolusNoopRepositoryCachePolicyProvider"/>.
    /// </summary>
    public static readonly IKyrolusRepositoryCachePolicyProvider Instance = new KyrolusNoopRepositoryCachePolicyProvider();

    /// <inheritdoc />
    public ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusRepositoryCachePolicyContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<KyrolusCachePolicy?>(null);
}
