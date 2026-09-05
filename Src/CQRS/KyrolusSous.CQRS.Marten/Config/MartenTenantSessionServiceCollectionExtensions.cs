using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.Marten.MultiTenancy;

namespace KyrolusSous.CQRS.Marten.Config;

/// <summary>
/// Registers <see cref="KyrolusMartenTenantSessionFactory"/> as the session factory Marten's own DI
/// integration uses to build every <see cref="IDocumentSession"/>/<see cref="IQuerySession"/> it hands out.
/// </summary>
public static class MartenTenantSessionServiceCollectionExtensions
{
    /// <summary>
    /// Opts into automatic per-tenant Marten session scoping, bridging
    /// <see cref="IKyrolusCurrentUserContext.TenantId"/> to Marten's native conjoined tenancy. See
    /// <see cref="KyrolusMartenTenantSessionFactory"/> for the fail-closed semantics and the (separate,
    /// also required) <c>StoreOptions.Policies.AllDocumentsAreMultiTenanted()</c> step.
    /// </summary>
    /// <param name="martenConfiguration">The expression returned by <c>services.AddMarten(...)</c>.</param>
    /// <param name="lifetime">
    /// The DI lifetime for <see cref="KyrolusMartenTenantSessionFactory"/> itself. Deliberately defaults
    /// to <see cref="ServiceLifetime.Scoped"/> rather than Marten's own <c>BuildSessionsWith</c> default
    /// of <see cref="ServiceLifetime.Singleton"/>: this factory depends on
    /// <see cref="IKyrolusCurrentUserContext"/>, which is itself registered scoped throughout this
    /// codebase (see <c>AddKyrolusCqrsAuthorization</c>/<c>AddKyrolusCqrsTenantScoping</c> in
    /// <see cref="KyrolusSous.CQRS.Abstractions.Config.ServiceCollectionExtensions"/>). Registering this
    /// factory as a singleton against a scoped dependency is a captive-dependency bug waiting to happen -
    /// it would either fail DI scope validation outright, or worse, succeed and pin the tenant from
    /// whichever request first resolved it for the lifetime of the app.
    /// </param>
    /// <returns>The same <paramref name="martenConfiguration"/>, for chaining.</returns>
    public static global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression UseKyrolusTenantScopedSessions(
        this global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression martenConfiguration,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(martenConfiguration);

        martenConfiguration.Services.TryAddScoped<IKyrolusCurrentUserContext, KyrolusDefaultCurrentUserContext>();
        return martenConfiguration.BuildSessionsWith<KyrolusMartenTenantSessionFactory>(lifetime);
    }
}
