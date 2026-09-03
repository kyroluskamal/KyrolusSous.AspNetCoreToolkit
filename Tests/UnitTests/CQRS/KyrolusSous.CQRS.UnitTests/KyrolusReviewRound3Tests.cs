using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.EF.Command.Bulk;
using KyrolusSous.CQRS.EF.Command.SoftDelete;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.CQRS.Marten.Behaviors;
using KyrolusSous.CQRS.Validation;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Validation.Abstractions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>Regression tests for the third CQRS-only review round.</summary>
[Collection("ThrottlingSemaphores")]
public sealed class KyrolusReviewRound3Tests
{
    public sealed record SomeCommand(string Value) : IKyrolusCommand<string>;

    #region Validation: engine + per-request validators must both run (additive, not either/or)
    private sealed class FixedFailureValidator(string message) : IKyrolusRequestValidator<SomeCommand>
    {
        public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(SomeCommand request, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([new KyrolusValidationFailure(nameof(SomeCommand.Value), message)]);
    }

    [Fact(DisplayName = "Validation: a per-request validator still runs even when a validation engine is also registered")]
    public async Task Validation_EngineAndValidators_BothRun()
    {
        var engine = Substitute.For<IKyrolusValidationEngine>();
        engine.ValidateAsync(Arg.Any<SomeCommand>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([new KyrolusValidationFailure("EngineField", "engine failure")]));

        var behavior = new KyrolusValidationBehavior<SomeCommand, string>(
            [new FixedFailureValidator("validator failure")],
            engine);

        var exception = await Should.ThrowAsync<KyrolusValidationException>(
            () => behavior.Handle(new SomeCommand("x"), _ => Task.FromResult("ok"), CancellationToken.None));

        // Before this fix, the engine branch ran INSTEAD of the validators branch - a targeted
        // validator registered alongside an app-wide engine silently never ran. Both failures must
        // be present now.
        exception.Errors.ShouldContain(f => f.PropertyName == "EngineField");
        exception.Errors.ShouldContain(f => f.PropertyName == nameof(SomeCommand.Value));
    }

    [Fact(DisplayName = "Validation: passes through to the handler when neither engine nor validators report a failure")]
    public async Task Validation_NoFailures_CallsNext()
    {
        var engine = Substitute.For<IKyrolusValidationEngine>();
        engine.ValidateAsync(Arg.Any<SomeCommand>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]));

        var behavior = new KyrolusValidationBehavior<SomeCommand, string>(engine: engine);
        var result = await behavior.Handle(new SomeCommand("x"), _ => Task.FromResult("ok"), CancellationToken.None);

        result.ShouldBe("ok");
    }
    #endregion

    #region ExceptionMapping: cancellation must propagate, not be offered to mappers
    private sealed class CatchAllMapper : IKyrolusExceptionMapper<string>
    {
        public bool TryMap(Exception exception, out string response)
        {
            // Deliberately broad, mirroring a common naive "map everything to a generic failure"
            // implementation - exactly the shape that would swallow a cancellation if the behavior
            // ever offered it one.
            response = "mapped-generic-failure";
            return true;
        }
    }

    [Fact(DisplayName = "ExceptionMapping: a cancelled request propagates OperationCanceledException instead of being mapped")]
    public async Task ExceptionMapping_Cancellation_PropagatesInsteadOfBeingMapped()
    {
        var behavior = new KyrolusExceptionMappingBehavior<SomeCommand, string>([new CatchAllMapper()]);
        using var cts = new CancellationTokenSource();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            behavior.Handle(
                new SomeCommand("x"),
                _ =>
                {
                    cts.Cancel();
                    throw new OperationCanceledException(cts.Token);
                },
                cts.Token));
    }

    [Fact(DisplayName = "ExceptionMapping: a genuine failure still gets mapped by a registered mapper")]
    public async Task ExceptionMapping_RealFailure_StillGetsMapped()
    {
        var behavior = new KyrolusExceptionMappingBehavior<SomeCommand, string>([new CatchAllMapper()]);

        var result = await behavior.Handle(
            new SomeCommand("x"),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        result.ShouldBe("mapped-generic-failure");
    }
    #endregion

    #region Marten domain events: collection responses (AddRange/UpdateRange/BulkUpsert shape)
    private sealed class EventedEntity : IDomainEventSource
    {
        private readonly List<object> _events = [];
        public IReadOnlyCollection<object> DomainEvents => _events;
        public void Raise(object domainEvent) => _events.Add(domainEvent);
        public void ClearDomainEvents() => _events.Clear();
    }

    public sealed record RangeCommand : IKyrolusCommand<IReadOnlyList<EventedEntity>>;

    [Fact(DisplayName = "Marten domain events: dispatches events from every item of a collection response, not just a single-entity response")]
    public async Task MartenDomainEvents_DispatchesFromEachItemInACollectionResponse()
    {
        var entity1 = new EventedEntity();
        entity1.Raise("Event1");
        var entity2 = new EventedEntity();
        entity2.Raise("Event2");

        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        var behavior = new KyrolusMartenDomainEventsDispatchBehavior<RangeCommand, IReadOnlyList<EventedEntity>>(publisher);

        var result = await behavior.Handle(
            new RangeCommand(),
            _ => Task.FromResult<IReadOnlyList<EventedEntity>>([entity1, entity2]),
            CancellationToken.None);

        result.Count.ShouldBe(2);
        await publisher.Received(1).PublishAsync(Arg.Is<object>(e => e.Equals("Event1")), Arg.Any<CancellationToken>());
        await publisher.Received(1).PublishAsync(Arg.Is<object>(e => e.Equals("Event2")), Arg.Any<CancellationToken>());
        entity1.DomainEvents.ShouldBeEmpty();
        entity2.DomainEvents.ShouldBeEmpty();
    }
    #endregion

    #region Throttling: semaphore dictionary is bounded, not leaked
    [Fact(DisplayName = "Throttling: tracking many distinct throttle keys does not grow the semaphore dictionary without bound")]
    public void ThrottlingSemaphores_ManyDistinctKeys_StaysBounded()
    {
        KyrolusThrottlingBehavior<object, object>.ClearSemaphores();
        try
        {
            for (var i = 0; i < 10_500; i++)
            {
                // Never acquired, so CurrentCount == MaxConcurrency for every entry - all idle and
                // therefore eligible for eviction once the cap is exceeded.
                KyrolusThrottlingSemaphores.GetOrAdd($"round3-key-{i}", maxConcurrency: 1);
            }

            KyrolusThrottlingSemaphores.TrackedKeyCount.ShouldBeLessThanOrEqualTo(10_000);
        }
        finally
        {
            KyrolusThrottlingBehavior<object, object>.ClearSemaphores();
        }
    }

    [Fact(DisplayName = "Throttling: a key evicted while idle still throttles correctly the next time it's used")]
    public async Task ThrottlingSemaphores_KeyReusedAfterEviction_StillWorks()
    {
        KyrolusThrottlingBehavior<object, object>.ClearSemaphores();
        try
        {
            for (var i = 0; i < 10_500; i++)
            {
                KyrolusThrottlingSemaphores.GetOrAdd($"round3-churn-{i}", maxConcurrency: 1);
            }

            // Created first, idle the whole time, and far below the cap of the last keys added - this
            // one was almost certainly evicted. GetOrAdd must transparently hand back a fresh, fully
            // functional semaphore rather than a disposed or broken one.
            var semaphore = KyrolusThrottlingSemaphores.GetOrAdd("round3-churn-0", maxConcurrency: 1);
            var acquired = await semaphore.WaitAsync(TimeSpan.FromMilliseconds(50));
            acquired.ShouldBeTrue();
            semaphore.Release();
        }
        finally
        {
            KyrolusThrottlingBehavior<object, object>.ClearSemaphores();
        }
    }
    #endregion

    #region Repository resolution: only a "not registered" InvalidOperationException means "optional repo missing"
    public sealed class DummySoftDeletableEntity
    {
        public Guid Id { get; set; }
    }

    public sealed class DummyDbContext : DbContext;

    [Fact(DisplayName = "SoftDeleteByIdCommandHandler: a repository-resolution failure unrelated to registration propagates instead of being reported as 'soft delete unsupported'")]
    public async Task SoftDelete_UnrelatedInvalidOperationException_Propagates()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        uow.GetRepository<IKyrolusSingleKeySoftDeleteRepository<DummySoftDeletableEntity, Guid>>()
            .Returns(_ => throw new InvalidOperationException("The connection pool has been exhausted."));

        var handler = new SoftDeleteByIdCommandHandler<DummyDbContext, DummySoftDeletableEntity, Guid>(uow);
        var command = new SoftDeleteByIdCommand<DummySoftDeletableEntity, Guid>([Guid.NewGuid()]);

        // Before this fix, EVERY InvalidOperationException from GetRepository was caught here and
        // silently turned into "return false" - a genuine failure (a disposed scope, a broken custom
        // factory) would have been indistinguishable from "this entity just has no soft-delete repo".
        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact(DisplayName = "SoftDeleteByIdCommandHandler: a genuine 'repository not registered' failure still gracefully reports no soft-delete support")]
    public async Task SoftDelete_RepositoryGenuinelyNotRegistered_ReturnsFalse()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        uow.GetRepository<IKyrolusSingleKeySoftDeleteRepository<DummySoftDeletableEntity, Guid>>()
            .Returns(_ => throw new InvalidOperationException(
                $"Repository of type '{typeof(IKyrolusSingleKeySoftDeleteRepository<DummySoftDeletableEntity, Guid>).FullName}' is not registered."));

        var handler = new SoftDeleteByIdCommandHandler<DummyDbContext, DummySoftDeletableEntity, Guid>(uow);
        var command = new SoftDeleteByIdCommand<DummySoftDeletableEntity, Guid>([Guid.NewGuid()]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.ShouldBeFalse();
    }
    #endregion

    #region Property allow-list: Patch/BulkPatch/ExecuteUpdate can reject disallowed property names
    public sealed record UpdateFixtureRequest(Dictionary<string, object> Updates)
        : IKyrolusCommand<string>, IKyrolusPropertyUpdateRequest
    {
        public IReadOnlySet<string>? AllowedProperties { get; init; }

        IEnumerable<string> IKyrolusPropertyUpdateRequest.UpdatedPropertyNames => Updates.Keys;
    }

    [Fact(DisplayName = "PropertyAllowList: rejects a request naming a property outside its own allow-list")]
    public async Task PropertyAllowList_DisallowedProperty_Throws()
    {
        var behavior = new KyrolusPropertyAllowListBehavior<UpdateFixtureRequest, string>();
        var request = new UpdateFixtureRequest(new Dictionary<string, object> { ["IsAdmin"] = true })
        {
            AllowedProperties = new HashSet<string> { "DisplayName" }
        };

        await Should.ThrowAsync<KyrolusSecurityException>(
            () => behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None));
    }

    [Fact(DisplayName = "PropertyAllowList: allows a property name that matches the allow-list case-insensitively")]
    public async Task PropertyAllowList_CaseInsensitiveMatch_Passes()
    {
        var behavior = new KyrolusPropertyAllowListBehavior<UpdateFixtureRequest, string>();
        var request = new UpdateFixtureRequest(new Dictionary<string, object> { ["displayname"] = "Bob" })
        {
            AllowedProperties = new HashSet<string> { "DisplayName" }
        };

        var result = await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        result.ShouldBe("ok");
    }

    [Fact(DisplayName = "PropertyAllowList: a request with no allow-list configured is left untouched (opt-in, not mandatory)")]
    public async Task PropertyAllowList_NoAllowListConfigured_PassesThrough()
    {
        var behavior = new KyrolusPropertyAllowListBehavior<UpdateFixtureRequest, string>();
        var request = new UpdateFixtureRequest(new Dictionary<string, object> { ["AnythingAtAll"] = 1 });

        var result = await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

        result.ShouldBe("ok");
    }

    [Fact(DisplayName = "PropertyAllowList: BulkPatchCommand's allow-list is checked against every item's Updates, not just the first")]
    public async Task PropertyAllowList_BulkPatchCommand_ChecksEveryItem()
    {
        var behavior = new KyrolusPropertyAllowListBehavior<BulkPatchCommand<DummySoftDeletableEntity, Guid>, int>();
        var command = new BulkPatchCommand<DummySoftDeletableEntity, Guid>(
        [
            new KyrolusBulkPatchItem([Guid.NewGuid()], new Dictionary<string, object> { ["Id"] = Guid.NewGuid() }),
            new KyrolusBulkPatchItem([Guid.NewGuid()], new Dictionary<string, object> { ["SecretFlag"] = true })
        ])
        {
            AllowedProperties = new HashSet<string> { "Id" }
        };

        await Should.ThrowAsync<KyrolusSecurityException>(
            () => behavior.Handle(command, _ => Task.FromResult(2), CancellationToken.None));
    }
    #endregion
}
