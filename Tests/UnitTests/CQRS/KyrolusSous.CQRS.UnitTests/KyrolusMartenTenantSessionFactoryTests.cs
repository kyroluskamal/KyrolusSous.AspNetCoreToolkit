using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.Marten.MultiTenancy;
using Marten;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>
/// Covers the tenant-accessor bridging logic in <see cref="KyrolusMartenTenantSessionFactory"/>: does it
/// ask <see cref="IDocumentStore"/> for a session scoped to the right tenant, and does it fail closed
/// (never touching the store at all) when there is no usable current tenant. Full cross-tenant document
/// isolation itself is Marten's own conjoined-tenancy machinery (<c>TenancyStyle.Conjoined</c>), which is
/// exercised against a real Postgres instance in Marten's own test suite, not re-verified here - these
/// tests only cover the bridge this library adds on top of it.
/// </summary>
public sealed class KyrolusMartenTenantSessionFactoryTests
{
    [Fact(DisplayName = "MartenTenantSessionFactory: OpenSession opens a lightweight session for the current tenant")]
    public void OpenSession_UsesCurrentTenantId()
    {
        var store = Substitute.For<IDocumentStore>();
        var expectedSession = Substitute.For<IDocumentSession>();
        store.LightweightSession("tenant-a").Returns(expectedSession);

        var userContext = new KyrolusDefaultCurrentUserContext(tenantId: "tenant-a");
        var factory = new KyrolusMartenTenantSessionFactory(store, userContext);

        var session = factory.OpenSession();

        session.ShouldBeSameAs(expectedSession);
        store.Received(1).LightweightSession("tenant-a");
    }

    [Fact(DisplayName = "MartenTenantSessionFactory: QuerySession opens a query session for the current tenant")]
    public void QuerySession_UsesCurrentTenantId()
    {
        var store = Substitute.For<IDocumentStore>();
        var expectedSession = Substitute.For<IQuerySession>();
        store.QuerySession("tenant-b").Returns(expectedSession);

        var userContext = new KyrolusDefaultCurrentUserContext(tenantId: "tenant-b");
        var factory = new KyrolusMartenTenantSessionFactory(store, userContext);

        var session = factory.QuerySession();

        session.ShouldBeSameAs(expectedSession);
        store.Received(1).QuerySession("tenant-b");
    }

    [Fact(DisplayName = "MartenTenantSessionFactory: fails closed - OpenSession throws instead of opening an untenanted session when TenantId is null")]
    public void OpenSession_NullTenant_ThrowsAndNeverTouchesStore()
    {
        var store = Substitute.For<IDocumentStore>();
        var userContext = new KyrolusDefaultCurrentUserContext(tenantId: null);
        var factory = new KyrolusMartenTenantSessionFactory(store, userContext);

        Should.Throw<KyrolusSecurityException>(() => factory.OpenSession());

        // Not just "not called with a tenant" - not called AT ALL. A misconfigured accessor must never
        // reach Marten's session-opening API in any shape, tenanted or not.
        store.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact(DisplayName = "MartenTenantSessionFactory: fails closed - QuerySession throws instead of opening an untenanted session when TenantId is empty")]
    public void QuerySession_EmptyTenant_ThrowsAndNeverTouchesStore()
    {
        var store = Substitute.For<IDocumentStore>();
        var userContext = new KyrolusDefaultCurrentUserContext(tenantId: string.Empty);
        var factory = new KyrolusMartenTenantSessionFactory(store, userContext);

        Should.Throw<KyrolusSecurityException>(() => factory.QuerySession());

        store.ReceivedCalls().ShouldBeEmpty();
    }
}
