using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.MultiTenancy;

/// <summary>
/// Composite tenant resolver that evaluates an ordered chain of <see cref="IKyrolusTenantResolver"/> strategies
/// until a non-empty tenant identifier is resolved.
/// </summary>
public sealed class KyrolusCompositeTenantResolver : IKyrolusTenantResolver
{
    private readonly IEnumerable<IKyrolusTenantResolver> _resolvers;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusCompositeTenantResolver"/> class with a chain of resolvers.
    /// </summary>
    /// <param name="resolvers">The collection of tenant resolvers to execute in priority order.</param>
    public KyrolusCompositeTenantResolver(IEnumerable<IKyrolusTenantResolver> resolvers)
    {
        _resolvers = resolvers ?? throw new ArgumentNullException(nameof(resolvers));
    }

    /// <inheritdoc />
    public async ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
    {
        foreach (var resolver in _resolvers)
        {
            try
            {
                var tenantId = await resolver.ResolveTenantIdAsync(httpContext);
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    return tenantId;
                }
            }
            catch
            {
                // Fault tolerance: allow fallback to subsequent resolvers in the chain
            }
        }

        return null;
    }
}
