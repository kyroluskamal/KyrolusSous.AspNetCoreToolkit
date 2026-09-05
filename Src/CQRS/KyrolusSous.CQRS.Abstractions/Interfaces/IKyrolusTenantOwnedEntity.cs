namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a persisted entity type as belonging to exactly one tenant, so a provider-level extension
/// (EF Core's <c>ApplyKyrolusTenantQueryFilters</c>, Marten's conjoined-tenancy session bridge) can
/// automatically scope every query against it to the current tenant.
/// </summary>
/// <remarks>
/// This is deliberately a distinct marker from <see cref="IKyrolusTenantScopedRequest"/>, which is a
/// <em>request</em>-level marker consumed by <see cref="KyrolusTenantScopingBehavior{TRequest, TResponse}"/>
/// to reject a request naming a tenant other than the caller's. The two solve different halves of the
/// same problem and neither implies the other:
/// <list type="bullet">
///   <item><description>
///   <see cref="IKyrolusTenantScopedRequest"/> guards a request that carries an explicit tenant id as
///   payload - it stops a caller from asking for tenant B's data by naming tenant B, but it has no way
///   to stop a query that never named a tenant at all (a <c>GetAll</c>, a specification, a raw LINQ
///   query) from reading every tenant's rows, because a generic pipeline behavior cannot reach into
///   EF/Marten query construction.
///   </description></item>
///   <item><description>
///   <see cref="IKyrolusTenantOwnedEntity"/> closes exactly that gap, but only for entity types that opt
///   into it and only once the consuming application wires the provider-specific extension into its own
///   <c>DbContext</c>/store configuration - this library owns neither, so it cannot apply the filter by
///   itself.
///   </description></item>
/// </list>
/// An application typically wants both: the request-level guard to catch a tampered tenant id in the
/// payload, and the entity-level marker to guarantee that even a request with no tenant id in it at all
/// still cannot return another tenant's rows.
/// </remarks>
/// <seealso cref="IKyrolusTenantScopedRequest"/>
public interface IKyrolusTenantOwnedEntity
{
    /// <summary>
    /// The tenant this entity instance belongs to. An entity implementing this interface is expected to
    /// always have a non-null, non-empty value here once persisted - a provider-level filter built on top
    /// of it fails closed (returns no rows) rather than treat <see langword="null"/> here as "visible to
    /// every tenant".
    /// </summary>
    string? TenantId { get; }
}
