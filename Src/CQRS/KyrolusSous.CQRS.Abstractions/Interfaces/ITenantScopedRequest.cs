namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a request that carries an explicit tenant identifier, so
/// <see cref="Behaviors.KyrolusTenantScopingBehavior{TRequest, TResponse}"/> can verify it matches the
/// current user's tenant before the handler runs.
/// </summary>
/// <remarks>
/// This exists for the common shape where a tenant id travels as part of the request payload (a route
/// parameter bound into the command/query, say) rather than being looked up fresh from the current
/// user on every handler. Without a check, a caller who can influence that field - a tampered request
/// body, a stale client, a bug in a mapper - can ask for tenant B's data while authenticated as tenant
/// A's user, and nothing in the pipeline would ever notice.
/// </remarks>
public interface ITenantScopedRequest
{
    /// <summary>
    /// The tenant this request claims to operate on. <see langword="null"/> means the request does
    /// not carry an explicit tenant and only relies on whatever the handler enforces itself.
    /// </summary>
    string? TenantId { get; }
}
