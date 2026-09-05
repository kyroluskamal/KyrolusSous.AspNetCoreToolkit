using KyrolusSous.CQRS.Abstractions.Security;

namespace KyrolusSous.CQRS.Marten.MultiTenancy;

/// <summary>
/// Marten <see cref="ISessionFactory"/> that opens every DI-resolved <see cref="IDocumentSession"/> and
/// <see cref="IQuerySession"/> already scoped to the current caller's tenant, by bridging
/// <see cref="IKyrolusCurrentUserContext.TenantId"/> to Marten's own conjoined multi-tenancy.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a session factory and not a query filter.</b> Unlike EF Core, Marten already has first-class,
/// idiomatic per-tenant document isolation built in: mark a document (or every document, via
/// <c>StoreOptions.Policies.AllDocumentsAreMultiTenanted()</c>) as conjoined-tenanted, and every read and
/// write Marten performs through a session is scoped to whatever tenant that session was opened for -
/// enforced by Marten itself at the storage layer, not by a LINQ expression this library would have to
/// keep in sync with Marten's own query translation. Reinventing an ad-hoc per-query filter on top of
/// that, the way <c>KyrolusSous.CQRS.EF.Config.KyrolusTenantQueryFilterExtensions</c> does for EF Core
/// (which has no native multi-tenancy to defer to), would just be a second, competing, easier-to-get-wrong
/// mechanism. This type is deliberately just a bridge: it decides which tenant a session opens for, and
/// lets Marten do the actual isolation.
/// </para>
/// <para>
/// <b>Fails closed.</b> This library's own CQRS command/query handlers (<c>KyrolusMartenTransactionBehavior</c>
/// and every generic Add/Update/Remove/Get* handler in this package) take their <see cref="IDocumentSession"/>
/// via constructor injection - they never call <see cref="IDocumentStore.LightweightSession(System.Data.IsolationLevel)"/>
/// directly.
/// That means once this factory is wired in (see the two-step setup below), it is the ONLY place a
/// tenant id gets attached to a session anywhere in this pipeline, so getting its null-handling right is
/// everything. If <see cref="IKyrolusCurrentUserContext.TenantId"/> is <see langword="null"/> or empty -
/// context not populated, a background job with no ambient caller, a misconfigured identity provider -
/// this type throws <see cref="KyrolusSecurityException"/> rather than opening a session with no tenant.
/// Opening an untenanted session would NOT silently return every tenant's documents (Marten falls back to
/// its own default-tenant partition, not "no filter"), but it would silently return whatever ended up in
/// that default partition - easy to misread as "isolation is working" when it is actually running on
/// data nobody meant to be shared. Throwing turns a subtle cross-tenant leak into a loud, immediate
/// failure instead.
/// </para>
/// <para><b>Two-step opt-in setup</b>, both required - neither alone isolates anything:</para>
/// <list type="number">
///   <item><description>
///   Enable Marten's own conjoined tenancy in <c>StoreOptions</c>, e.g.
///   <c>options.Policies.AllDocumentsAreMultiTenanted();</c> (or <c>schema.For&lt;T&gt;().MultiTenanted()</c>
///   per document type). Without this, Marten treats every document as <c>TenancyStyle.Single</c> and this
///   factory's tenant id has nothing to scope.
///   </description></item>
///   <item><description>
///   Register this factory so Marten's DI-resolved sessions actually use it:
///   <c>services.AddMarten(...).UseKyrolusTenantScopedSessions();</c> (see
///   <see cref="KyrolusSous.CQRS.Marten.Config.MartenTenantSessionServiceCollectionExtensions.UseKyrolusTenantScopedSessions"/>).
///   Without this, <c>AddMarten</c>'s own default session factory keeps building untenanted sessions and
///   this type is never consulted.
///   </description></item>
/// </list>
/// </remarks>
public sealed class KyrolusMartenTenantSessionFactory(
    IDocumentStore store,
    IKyrolusCurrentUserContext userContext) : ISessionFactory
{
    private readonly IDocumentStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IKyrolusCurrentUserContext _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    /// <inheritdoc />
    public IQuerySession QuerySession() => _store.QuerySession(ResolveTenantIdOrThrow());

    /// <inheritdoc />
    public IDocumentSession OpenSession() => _store.LightweightSession(ResolveTenantIdOrThrow());

    private string ResolveTenantIdOrThrow()
    {
        var tenantId = _userContext.TenantId;
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new KyrolusSecurityException(
                "[Kyrolus CQRS Security] Cannot open a tenant-scoped Marten session: the current user " +
                "context has no TenantId. Refusing to open an untenanted session, which would silently " +
                "read and write Marten's default tenant partition instead of failing loudly.");
        }

        return tenantId;
    }
}
