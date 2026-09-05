using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusDefaultCacheKeyProviderTests
{
    public sealed record DeleteUserCommand(Guid Id) : IKyrolusCommand;

    public sealed record CreateUserCommand(string Email) : IKyrolusCommand<Guid>;

    public sealed record RemoveUserCommand(Guid Id) : IKyrolusCommand<bool>;

    public sealed record CreateOrderCommandWithOverride(string CustomerId) : IKyrolusCommand<Guid>
    {
        public string InvalidatesCachePattern => "Order";
    }

    public sealed record GetOrdersByStatusQuery(string Status) : IKyrolusQuery<string>;

    public sealed record GetAllOrdersQuery : IKyrolusQuery<string>;

    public sealed record GetOrderByIdQuery(Guid Id) : IKyrolusQuery<string>;

    public sealed record GetOrdersPagedQuery(int PageNumber, int PageSize) : IKyrolusQuery<string>;

    public sealed record GetOrdersCursorQuery(string Cursor) : IKyrolusQuery<string>;

    [Fact(DisplayName = "GetCachePattern: a non-generic IKyrolusCommand derives its pattern from its own type name via the verb+Command heuristic, instead of returning the type name unchanged")]
    public void GetCachePattern_NonGenericCommand_DerivesPatternViaHeuristic()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var pattern = provider.GetCachePattern(new DeleteUserCommand(Guid.NewGuid()));

        pattern.ShouldBe("User");
    }

    [Fact(DisplayName = "GetCachePattern: IKyrolusCommand<Guid> (e.g. a create command returning the new id) resolves to the real entity name via the heuristic, not the scalar response type name")]
    public void GetCachePattern_GuidResponseCommand_DerivesRealEntityNameViaHeuristic()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var pattern = provider.GetCachePattern(new CreateUserCommand("a@b.com"));

        // Before the fix this returned "Guid" - KyrolusCommandCacheInvalidationBehavior's wildcard
        // pattern would then never match the real "User_*" cached keys, so invalidation silently did
        // nothing after every create-shaped write.
        pattern.ShouldBe("User");
    }

    [Fact(DisplayName = "GetCachePattern: IKyrolusCommand<bool> (e.g. a delete/success-flag command) resolves to the real entity name via the heuristic, not the scalar response type name")]
    public void GetCachePattern_BoolResponseCommand_DerivesRealEntityNameViaHeuristic()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var pattern = provider.GetCachePattern(new RemoveUserCommand(Guid.NewGuid()));

        // Before the fix this returned "Boolean".
        pattern.ShouldBe("User");
    }

    [Fact(DisplayName = "GetCachePattern: an explicit InvalidatesCachePattern property wins over the heuristic, verbatim")]
    public void GetCachePattern_ExplicitInvalidatesCachePattern_UsedVerbatim()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var pattern = provider.GetCachePattern(new CreateOrderCommandWithOverride("cust-1"));

        pattern.ShouldBe("Order");
    }

    [Fact(DisplayName = "GetCacheKey: two instances of the same filtered-query type with different filter values produce different cache keys")]
    public void GetCacheKey_SameQueryTypeDifferentFilterValue_ProducesDifferentKeys()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var pendingKey = provider.GetCacheKey(new GetOrdersByStatusQuery("Pending"));
        var shippedKey = provider.GetCacheKey(new GetOrdersByStatusQuery("Shipped"));

        // Before the fix, both fell through to the same "{entityName}_{requestName}" fallback
        // regardless of Status, so the second query would be served the first query's cached result.
        pendingKey.ShouldNotBeNull();
        shippedKey.ShouldNotBeNull();
        pendingKey.ShouldNotBe(shippedKey);
    }

    [Fact(DisplayName = "GetCacheKey: the same filter value produces the same cache key (deterministic)")]
    public void GetCacheKey_SameFilterValue_IsDeterministic()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var first = provider.GetCacheKey(new GetOrdersByStatusQuery("Pending"));
        var second = provider.GetCacheKey(new GetOrdersByStatusQuery("Pending"));

        first.ShouldBe(second);
    }

    [Fact(DisplayName = "GetCacheKey: GetAll-shaped query keeps its original key format, unaffected by the fingerprint fallback")]
    public void GetCacheKey_GetAllShape_Unchanged()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var key = provider.GetCacheKey(new GetAllOrdersQuery());

        key.ShouldBe("String_GetAll");
    }

    [Fact(DisplayName = "GetCacheKey: ById-shaped query keeps its original key format, unaffected by the fingerprint fallback")]
    public void GetCacheKey_ByIdShape_Unchanged()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();
        var id = Guid.NewGuid();

        var key = provider.GetCacheKey(new GetOrderByIdQuery(id));

        key.ShouldBe($"String_GetById_{id}");
    }

    [Fact(DisplayName = "GetCacheKey: paged query keeps its original key format, unaffected by the fingerprint fallback")]
    public void GetCacheKey_PagedShape_Unchanged()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var key = provider.GetCacheKey(new GetOrdersPagedQuery(2, 25));

        key.ShouldBe("String_GetOrdersPagedQuery_p2_s25");
    }

    [Fact(DisplayName = "GetCacheKey: cursor-shaped query keeps its original key format, unaffected by the fingerprint fallback")]
    public void GetCacheKey_CursorShape_Unchanged()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var key = provider.GetCacheKey(new GetOrdersCursorQuery("abc123"));

        key.ShouldBe("String_GetOrdersCursorQuery_cabc123");
    }
}
