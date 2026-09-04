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

    [Fact(DisplayName = "CommandCacheInvalidation: a cacheable non-generic IKyrolusCommand invalidates using a pattern derived from its own type")]
    public async Task NonGenericCommand_WithProvider_InvalidatesUsingOwnTypeAsPattern()
    {
        var cacheProvider = Substitute.For<IKyrolusCacheProvider>();
        var behavior = new KyrolusCommandCacheInvalidationBehavior<DeleteUserCommand, Unit>(new KyrolusDefaultCacheKeyProvider(), cacheProvider);
        var command = new DeleteUserCommand(Guid.NewGuid());

        await behavior.Handle(command, _ => Task.FromResult(Unit.Value), CancellationToken.None);

        // Before the KyrolusDefaultCacheKeyProvider fix, GetCachePattern returned null for a plain
        // IKyrolusCommand (no TResponse to derive an entity name from), so RemoveKeysByPatternAsync
        // was never called at all.
        await cacheProvider.Received(1).RemoveKeysByPatternAsync("*DeleteUserCommand*", Arg.Any<CancellationToken>());
    }
}
