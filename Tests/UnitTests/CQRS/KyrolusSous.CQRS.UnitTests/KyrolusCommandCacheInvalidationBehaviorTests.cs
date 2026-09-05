using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusCommandCacheInvalidationBehaviorTests
{
    public sealed record DeleteUserCommand(Guid Id) : IKyrolusCommand, IKyrolusCacheableRequest
    {
        public bool Cacheable { get; set; } = true;
    }

    [Fact(DisplayName = "CommandCacheInvalidation: no IKyrolusCacheProvider registered - behavior no-ops instead of throwing")]
    public async Task NoCacheProvider_DoesNotThrow_AndStillReturnsResponse()
    {
        var behavior = new KyrolusCommandCacheInvalidationBehavior<DeleteUserCommand, Unit>(new KyrolusDefaultCacheKeyProvider());
        var command = new DeleteUserCommand(Guid.NewGuid());

        var result = await behavior.Handle(command, _ => Task.FromResult(Unit.Value), CancellationToken.None);

        result.ShouldBe(Unit.Value);
    }

    [Fact(DisplayName = "CommandCacheInvalidation: a cacheable non-generic IKyrolusCommand invalidates using the entity name derived from its own type via the verb+Command heuristic")]
    public async Task NonGenericCommand_WithProvider_InvalidatesUsingHeuristicEntityName()
    {
        var cacheProvider = Substitute.For<IKyrolusCacheProvider>();
        var behavior = new KyrolusCommandCacheInvalidationBehavior<DeleteUserCommand, Unit>(new KyrolusDefaultCacheKeyProvider(), cacheProvider);
        var command = new DeleteUserCommand(Guid.NewGuid());

        await behavior.Handle(command, _ => Task.FromResult(Unit.Value), CancellationToken.None);

        // Before the KyrolusDefaultCacheKeyProvider fix, GetCachePattern returned null for a plain
        // IKyrolusCommand (no TResponse to derive an entity name from), so RemoveKeysByPatternAsync
        // was never called at all. After that fix but before the naming-heuristic fix, it returned the
        // command's own type name unchanged ("DeleteUserCommand"), which still does not match the real
        // "User_*" cached query keys - only the "*User*" wildcard derived below does.
        await cacheProvider.Received(1).RemoveKeysByPatternAsync("*User*", Arg.Any<CancellationToken>());
    }

    public sealed record CreateOrderCommand(string CustomerId) : IKyrolusCommand<Guid>, IKyrolusCacheableRequest
    {
        public bool Cacheable { get; set; } = true;
    }

    [Fact(DisplayName = "CommandCacheInvalidation: a command whose TResponse is a scalar (e.g. IKyrolusCommand<Guid>) invalidates using the real entity name, not the scalar type name")]
    public async Task ScalarResponseCommand_WithProvider_InvalidatesUsingRealEntityName()
    {
        var cacheProvider = Substitute.For<IKyrolusCacheProvider>();
        var behavior = new KyrolusCommandCacheInvalidationBehavior<CreateOrderCommand, Guid>(new KyrolusDefaultCacheKeyProvider(), cacheProvider);
        var command = new CreateOrderCommand("cust-1");

        await behavior.Handle(command, _ => Task.FromResult(Guid.NewGuid()), CancellationToken.None);

        // Before the fix, GetCachePattern resolved "Guid" (the response type) for this shape, so the
        // wildcard pattern "*Guid*" never matched the real "Order_*" cached query keys and invalidation
        // silently did nothing after every Create-shaped write.
        await cacheProvider.Received(1).RemoveKeysByPatternAsync("*Order*", Arg.Any<CancellationToken>());
    }
}
