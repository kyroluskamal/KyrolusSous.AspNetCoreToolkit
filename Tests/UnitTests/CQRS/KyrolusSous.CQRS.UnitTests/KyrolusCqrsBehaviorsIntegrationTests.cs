using System.Reflection;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.CQRS.Validation;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Mediator.Runtime.GeneratorIntegration;
using KyrolusSous.Mediator.Runtime.Implementations;
using KyrolusSous.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact(DisplayName = "PipelineOrder: ValidationBehavior (-950) sits outside Performance/Audit/Idempotency/Throttling (-900/-850/-800/-750)")]
    public void ValidationBehavior_PipelineOrder_RunsBeforeAuditIdempotencyThrottling()
    {
        var validationOrder = typeof(KyrolusValidationBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;

        validationOrder.ShouldBe(-950);
        validationOrder.ShouldBeLessThan(typeof(KyrolusPerformanceAndTelemetryBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order);
        validationOrder.ShouldBeLessThan(typeof(KyrolusAuditBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order);
        validationOrder.ShouldBeLessThan(typeof(KyrolusIdempotencyBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order);
        validationOrder.ShouldBeLessThan(typeof(KyrolusThrottlingBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order);
    }

    public sealed record ValidatedIdempotentCommand(string Name, string IdempotencyKey) : IIdempotentCommand<string>
    {
        public TimeSpan? IdempotencyTtl => TimeSpan.FromMinutes(30);
    }

    public sealed class ValidatedIdempotentCommandValidator : IKyrolusRequestValidator<ValidatedIdempotentCommand>
    {
        public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(ValidatedIdempotentCommand request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                IReadOnlyList<KyrolusValidationFailure> failures = [new("Name", "Name is required", null, KyrolusValidationSeverity.Error, "ERR_EMPTY")];
                return ValueTask.FromResult(failures);
            }
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);
        }
    }

    [Fact(DisplayName = "PipelineOrder: an invalid request is rejected before it ever reaches the idempotency behavior")]
    public async Task ValidationBehavior_NestedOutsideIdempotency_RejectsBeforeIdempotencyRuns()
    {
        var cache = Substitute.For<IKyrolusCacheProvider>();
        // Inner behavior: PipelineOrder(-800), the mediator resolves and runs this closest to the handler.
        var idempotency = new KyrolusIdempotencyBehavior<ValidatedIdempotentCommand, string>(cache);
        // Outer behavior: PipelineOrder(-950), the mediator resolves and runs this first.
        var validation = new KyrolusValidationBehavior<ValidatedIdempotentCommand, string>([new ValidatedIdempotentCommandValidator()]);

        var invalidCommand = new ValidatedIdempotentCommand("", "key-1");
        var handlerRan = false;
        RequestHandlerDelegate<string> innerNext = ct => idempotency.Handle(
            invalidCommand,
            _ =>
            {
                handlerRan = true;
                return Task.FromResult("done");
            },
            ct);

        await Should.ThrowAsync<KyrolusValidationException>(
            () => validation.Handle(invalidCommand, innerNext, CancellationToken.None));

        handlerRan.ShouldBeFalse("the actual handler must never run for an invalid request");
        await cache.DidNotReceive().GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<KyrolusCacheEntryOptions>(), Arg.Any<CancellationToken>());
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

    [Fact(DisplayName = "PipelineOrder: ExceptionMappingBehavior (-2100) sits outside RequestExceptionProcessorBehavior (-2000)")]
    public void ExceptionMappingBehavior_PipelineOrder_IsMoreNegativeThan_RequestExceptionProcessorBehavior()
    {
        var mappingOrder = typeof(KyrolusExceptionMappingBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;
        var processorOrder = typeof(KyrolusRequestExceptionProcessorBehavior<,>).GetCustomAttribute<PipelineOrderAttribute>()!.Order;

        mappingOrder.ShouldBe(-2100);
        processorOrder.ShouldBe(-2000);
        mappingOrder.ShouldBeLessThan(processorOrder);
    }

    /// <summary>Fake dispatch source standing in for the source-generated one, recording that its action ran.</summary>
    private sealed class RecordingActionDispatchSource(Action onActionInvoked) : IKyrolusRequestExceptionDispatchSource
    {
        public IReadOnlyList<(Type ActionType, Func<CancellationToken, Task> Invoke)>? CreateActionInvocations(
            Type requestType, Type exceptionType, object request, Exception exception, IServiceProvider serviceProvider)
        {
            if (exceptionType == typeof(InvalidOperationException))
            {
                return [(typeof(RecordingActionDispatchSource), _ =>
                {
                    onActionInvoked();
                    return Task.CompletedTask;
                })];
            }
            return null;
        }

        // No handler recovers, so the processor behavior rethrows and the mapping behavior gets its turn.
        public IReadOnlyList<Func<CancellationToken, Task>>? CreateHandlerInvocations(
            Type requestType, Type exceptionType, Type responseType, object request, Exception exception, object state, IServiceProvider serviceProvider)
            => null;
    }

    [Fact(DisplayName = "PipelineOrder: exception action runs first, mapper is the last line of defense, when nested per the fixed orders")]
    public async Task ExceptionMappingBehavior_NestedOutsideProcessor_ActionRunsThenMapperRecovers()
    {
        var actionRan = false;
        var services = new ServiceCollection();
        services.AddSingleton<IKyrolusRequestExceptionDispatchSource>(new RecordingActionDispatchSource(() => actionRan = true));
        await using var provider = services.BuildServiceProvider();

        // Inner behavior: PipelineOrder(-2000), the mediator resolves and runs this closest to the handler.
        var processor = new KyrolusRequestExceptionProcessorBehavior<ValidatedCommand, string>(provider);

        var mapper = Substitute.For<IKyrolusExceptionMapper<string>>();
        mapper.TryMap(Arg.Any<Exception>(), out Arg.Any<string>()!)
            .Returns(x =>
            {
                x[1] = "MappedErrorResponse";
                return true;
            });

        // Outer behavior: PipelineOrder(-2100), the mediator resolves and runs this first.
        var mapping = new KyrolusExceptionMappingBehavior<ValidatedCommand, string>([mapper]);

        var request = new ValidatedCommand("test");
        RequestHandlerDelegate<string> innerNext = ct =>
            processor.Handle(request, _ => throw new InvalidOperationException("boom"), ct);

        var result = await mapping.Handle(request, innerNext, CancellationToken.None);

        actionRan.ShouldBeTrue("the exception action registered on the processor behavior should run before the mapper gets a chance");
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
