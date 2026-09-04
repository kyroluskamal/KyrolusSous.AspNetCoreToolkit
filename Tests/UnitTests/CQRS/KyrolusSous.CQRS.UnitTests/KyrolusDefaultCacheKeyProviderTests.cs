using KyrolusSous.CQRS.Caching;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusDefaultCacheKeyProviderTests
{
    public sealed record DeleteUserCommand(Guid Id) : IKyrolusCommand;

    public sealed record CreateUserCommand(string Email) : IKyrolusCommand<Guid>;

    [Fact(DisplayName = "GetCachePattern: a non-generic IKyrolusCommand derives its pattern from its own type, instead of returning null")]
    public void GetCachePattern_NonGenericCommand_DerivesPatternFromOwnType()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var pattern = provider.GetCachePattern(new DeleteUserCommand(Guid.NewGuid()));

        pattern.ShouldBe(nameof(DeleteUserCommand));
    }

    [Fact(DisplayName = "GetCachePattern: a generic IKyrolusCommand<TResponse> still derives its pattern from the response type (unchanged)")]
    public void GetCachePattern_GenericCommand_StillDerivesFromResponseType()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();

        var pattern = provider.GetCachePattern(new CreateUserCommand("a@b.com"));

        pattern.ShouldBe(nameof(Guid));
    }
}
