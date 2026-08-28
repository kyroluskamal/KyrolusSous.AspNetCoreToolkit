using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.CQRS.Validation;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Validation.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusCqrsBehaviorsIntegrationTests
{
    public sealed record ValidatedCommand(string Name) : IKyrolusCommand<string>;

    public sealed class ValidatedCommandValidator : IKyrolusRequestValidator<ValidatedCommand>
    {
        public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(ValidatedCommand request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                IReadOnlyList<KyrolusValidationFailure> failures = [new("Name", "Name is required", null, KyrolusValidationSeverity.Error, "ERR_EMPTY")];
                return ValueTask.FromResult(failures);
            }
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
        }
    }

    [Fact(DisplayName = "ValidationBehavior: Passes valid request")]
    public async Task ValidationBehavior_ValidRequest_Proceeds()
    {
        var validator = new ValidatedCommandValidator();
        var behavior = new KyrolusValidationBehavior<ValidatedCommand, string>([validator]);

        var cmd = new ValidatedCommand("Valid Name");
        var result = await behavior.Handle(cmd, ct => Task.FromResult("OK"), CancellationToken.None);

        result.ShouldBe("OK");
    }

    [Fact(DisplayName = "ValidationBehavior: Throws KyrolusValidationException for invalid request")]
    public async Task ValidationBehavior_InvalidRequest_ThrowsException()
    {
        var validator = new ValidatedCommandValidator();
        var behavior = new KyrolusValidationBehavior<ValidatedCommand, string>([validator]);

        var cmd = new ValidatedCommand("");

        var ex = await Should.ThrowAsync<KyrolusValidationException>(async () =>
        {
            await behavior.Handle(cmd, ct => Task.FromResult("OK"), CancellationToken.None);
        });

        ex.Errors.ShouldNotBeEmpty();
        ex.Errors.First().PropertyName.ShouldBe("Name");
    }

    [Fact(DisplayName = "ExceptionMappingBehavior: Maps exception via IKyrolusExceptionMapper")]
    public async Task ExceptionMappingBehavior_MapsException()
    {
        var mapper = Substitute.For<IKyrolusExceptionMapper<string>>();
        mapper.TryMap(Arg.Any<Exception>(), out Arg.Any<string>()!)
            .Returns(x =>
            {
                x[1] = "MappedErrorResponse";
                return true;
            });

        var behavior = new KyrolusExceptionMappingBehavior<ValidatedCommand, string>([mapper]);

        var result = await behavior.Handle(
            new ValidatedCommand("test"),
            ct => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        result.ShouldBe("MappedErrorResponse");
    }

    public sealed class CachedQuery : ICacheableRequest, IKyrolusQuery<string>
    {
        public CachedQuery(string id) => Id = id;
        public string Id { get; set; }
        public bool Cacheable { get; set; } = true;
    }

    [Fact(DisplayName = "QueryCachingBehavior: Caches query response")]
    public async Task QueryCachingBehavior_CachesResult()
    {
        var cache = Substitute.For<IKyrolusCacheProvider>();
        cache.GetAsync<string>("query-cache-123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        var keyProvider = Substitute.For<IKyrolusCacheKeyProvider>();
        keyProvider.GetCacheKey(Arg.Any<CachedQuery>()).Returns("query-cache-123");

        var behavior = new KyrolusQueryCachingBehavior<CachedQuery, string>(cache, keyProvider);

        var query = new CachedQuery("123");
        var result = await behavior.Handle(query, ct => Task.FromResult("data-payload"), CancellationToken.None);

        result.ShouldBe("data-payload");
        await cache.Received(1).SetAsync(
            "query-cache-123",
            "data-payload",
            TimeSpan.Zero,
            Arg.Any<CancellationToken>());
    }
}
