using KyrolusSous.CQRS.Saga;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusSagaTests
{
    public sealed record TestSagaContext
    {
        public List<string> Log { get; set; } = [];
        public string? ReservationId { get; set; }
    }

    private sealed class RecordingStep(string name, bool failExecute = false, bool failCompensate = false)
        : IKyrolusSagaStep<TestSagaContext>
    {
        public string Name => name;

        public Task ExecuteAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            if (failExecute) throw new InvalidOperationException($"{name} execute failed");
            context.Log.Add($"execute:{name}");
            if (name == "Reserve") context.ReservationId = "res-1";
            return Task.CompletedTask;
        }

        public Task CompensateAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            if (failCompensate) throw new InvalidOperationException($"{name} compensate failed");
            context.Log.Add($"compensate:{name}");
            return Task.CompletedTask;
        }
    }

    private sealed class TestSaga(IReadOnlyList<IKyrolusSagaStep<TestSagaContext>> steps) : KyrolusSagaDefinition<TestSagaContext>
    {
        public override string SagaName => "TestSaga";
        protected override IReadOnlyList<IKyrolusSagaStep<TestSagaContext>> Steps { get; } = steps;
    }

    [Fact(DisplayName = "Saga: all steps succeeding completes the saga and preserves context written along the way")]
    public async Task Saga_AllStepsSucceed_Completes()
    {
        var store = new InMemorySagaStore();
        var registry = new KyrolusSagaDefinitionRegistry([]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);
        var saga = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("Charge"), new RecordingStep("Ship")]);

        var sagaId = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None);

        var instance = await store.GetAsync(sagaId);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Completed);
        instance.CompletedAtUtc.ShouldNotBeNull();

        var context = (TestSagaContext)saga.DeserializeContext(instance.ContextJson);
        context.Log.ShouldBe(["execute:Reserve", "execute:Charge", "execute:Ship"]);
        context.ReservationId.ShouldBe("res-1"); // a mutation a step made, correctly persisted
    }

    [Fact(DisplayName = "Saga: a failing step compensates completed steps in reverse order")]
    public async Task Saga_StepFails_CompensatesCompletedStepsInReverse()
    {
        var store = new InMemorySagaStore();
        var registry = new KyrolusSagaDefinitionRegistry([]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);
        var saga = new TestSaga([
            new RecordingStep("Reserve"),
            new RecordingStep("Charge"),
            new RecordingStep("Ship", failExecute: true)
        ]);

        var sagaId = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None);

        var instance = await store.GetAsync(sagaId);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Compensated);
        instance.Error.ShouldNotBeNull();
        instance.Error.ShouldContain("Ship execute failed");

        var context = (TestSagaContext)saga.DeserializeContext(instance.ContextJson);
        // Reserve and Charge executed (Ship never did, so it is never compensated), then undone
        // Charge-first: reverse of execution order.
        context.Log.ShouldBe(["execute:Reserve", "execute:Charge", "compensate:Charge", "compensate:Reserve"]);
    }

    [Fact(DisplayName = "Saga: a failing compensation marks the saga Failed and stops instead of skipping ahead")]
    public async Task Saga_CompensationFails_MarksFailedAndStops()
    {
        var store = new InMemorySagaStore();
        var registry = new KyrolusSagaDefinitionRegistry([]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);
        var saga = new TestSaga([
            new RecordingStep("Reserve"),
            new RecordingStep("Charge", failCompensate: true),
            new RecordingStep("Ship", failExecute: true)
        ]);

        var sagaId = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None);

        var instance = await store.GetAsync(sagaId);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Failed);
        instance.Error.ShouldNotBeNull();
        instance.Error.ShouldContain("Compensation failed at step 1");

        var context = (TestSagaContext)saga.DeserializeContext(instance.ContextJson);
        // Charge's compensation threw before Reserve's ever ran - Reserve must not be silently skipped.
        context.Log.ShouldBe(["execute:Reserve", "execute:Charge"]);
    }

    [Fact(DisplayName = "Saga: retrying a failed compensation continues from where it stopped, then finishes")]
    public async Task Saga_RetryCompensation_ContinuesFromWhereItStopped()
    {
        var store = new InMemorySagaStore();

        // Charge's compensation fails the first time only.
        var chargeShouldFail = true;
        var reserveStep = new RecordingStep("Reserve");
        var flakyChargeStep = new FlakyCompensateStep("Charge", () => chargeShouldFail);
        var saga = new TestSaga([reserveStep, flakyChargeStep, new RecordingStep("Ship", failExecute: true)]);
        var registry = new KyrolusSagaDefinitionRegistry([saga]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);

        var sagaId = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None);
        (await store.GetAsync(sagaId))!.Status.ShouldBe(KyrolusSagaStatus.Failed);

        chargeShouldFail = false;
        await coordinator.RetryCompensationAsync(sagaId, CancellationToken.None);

        var instance = await store.GetAsync(sagaId);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Compensated);

        var context = (TestSagaContext)saga.DeserializeContext(instance.ContextJson);
        context.Log.ShouldBe(["execute:Reserve", "execute:Charge", "compensate:Charge", "compensate:Reserve"]);
    }

    private sealed class FlakyCompensateStep(string name, Func<bool> shouldFail) : IKyrolusSagaStep<TestSagaContext>
    {
        public string Name => name;

        public Task ExecuteAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            context.Log.Add($"execute:{name}");
            return Task.CompletedTask;
        }

        public Task CompensateAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            if (shouldFail()) throw new InvalidOperationException($"{name} compensate failed");
            context.Log.Add($"compensate:{name}");
            return Task.CompletedTask;
        }
    }

    [Fact(DisplayName = "Saga: resuming after a restart continues from the persisted step, without re-running completed ones")]
    public async Task Saga_ResumeIncomplete_ContinuesFromPersistedStep()
    {
        var store = new InMemorySagaStore();
        var reserveStep = new RecordingStep("Reserve");
        var chargeStep = new RecordingStep("Charge");
        var shipStep = new RecordingStep("Ship");
        var saga = new TestSaga([reserveStep, chargeStep, shipStep]);
        var registry = new KyrolusSagaDefinitionRegistry([saga]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);

        // Simulate a process that crashed after step 0 completed but before step 1 ran: seed the
        // store directly, as if StartAsync had run and persisted after Reserve, then the process died.
        var context = new TestSagaContext { Log = ["execute:Reserve"], ReservationId = "res-1" };
        var crashedInstance = new KyrolusSagaInstance
        {
            SagaName = saga.SagaName,
            ContextJson = saga.SerializeContext(context),
            CurrentStepIndex = 1,
            Status = KyrolusSagaStatus.Running
        };
        await store.SaveAsync(crashedInstance);

        var resumedCount = await coordinator.ResumeIncompleteAsync(CancellationToken.None);

        resumedCount.ShouldBe(1);
        var instance = await store.GetAsync(crashedInstance.Id);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Completed);

        var finalContext = (TestSagaContext)saga.DeserializeContext(instance.ContextJson);
        // "execute:Reserve" appears exactly once - Reserve was NOT re-run on resume.
        finalContext.Log.ShouldBe(["execute:Reserve", "execute:Charge", "execute:Ship"]);
    }

    [Fact(DisplayName = "Saga: an instance whose saga name is not registered is skipped, not thrown, during resume")]
    public async Task Saga_ResumeIncomplete_SkipsUnregisteredSagaName()
    {
        var store = new InMemorySagaStore();
        var registry = new KyrolusSagaDefinitionRegistry([]); // nothing registered
        var coordinator = new KyrolusSagaCoordinator(store, registry);

        await store.SaveAsync(new KyrolusSagaInstance
        {
            SagaName = "UnknownSaga",
            ContextJson = "{}",
            Status = KyrolusSagaStatus.Running
        });

        var resumedCount = await coordinator.ResumeIncompleteAsync(CancellationToken.None);
        resumedCount.ShouldBe(0);
    }

    private sealed class CancelingStep(string name, CancellationTokenSource cancelOnExecute) : IKyrolusSagaStep<TestSagaContext>
    {
        public string Name => name;

        public Task ExecuteAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            // Simulates the saga's own token being cancelled mid-flight (e.g. app shutdown) and the
            // step honoring it idiomatically - not a pre-cancelled token from the very start, which
            // would trip ThrowIfCancellationRequested before the earlier step ever got to run.
            cancelOnExecute.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }

        public Task CompensateAsync(TestSagaContext context, CancellationToken cancellationToken)
            => throw new OperationCanceledException(cancellationToken);
    }

    [Fact(DisplayName = "Saga: cancellation from a step propagates instead of being treated as a step failure that triggers compensation")]
    public async Task Saga_StepCancellation_PropagatesInsteadOfCompensating()
    {
        var store = new InMemorySagaStore();
        var registry = new KyrolusSagaDefinitionRegistry([]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);
        using var cts = new CancellationTokenSource();
        var saga = new TestSaga([new RecordingStep("Reserve"), new CancelingStep("Charge", cts)]);

        await Should.ThrowAsync<OperationCanceledException>(
            () => coordinator.StartAsync(saga, new TestSagaContext(), cts.Token));

        // Reserve completed and was persisted as such; Charge never got the chance to run "for real" -
        // it must not have been compensated, since compensation is only for steps whose forward
        // action actually completed, and cancellation is not that.
        var all = store.AllInstances;
        all.Count.ShouldBe(1);
        all.Single().Status.ShouldBe(KyrolusSagaStatus.Running);
        var context = (TestSagaContext)saga.DeserializeContext(all.Single().ContextJson);
        context.Log.ShouldBe(["execute:Reserve"]);
    }

    [Fact(DisplayName = "Saga: RetryCompensationAsync leaves the instance Failed (not corrupted) if the stored context cannot be deserialized")]
    public async Task Saga_RetryCompensation_LeavesInstanceFailedWhenContextIsCorrupt()
    {
        var store = new InMemorySagaStore();
        var saga = new TestSaga([new RecordingStep("Reserve")]);
        var registry = new KyrolusSagaDefinitionRegistry([saga]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);

        var corrupted = new KyrolusSagaInstance
        {
            SagaName = saga.SagaName,
            ContextJson = "{not valid json", // deliberately corrupt
            CurrentStepIndex = 1,
            Status = KyrolusSagaStatus.Failed,
            Error = "original diagnostic"
        };
        await store.SaveAsync(corrupted);

        await Should.ThrowAsync<Exception>(() => coordinator.RetryCompensationAsync(corrupted.Id, CancellationToken.None));

        // Must still be Failed with its original diagnostic - not stuck as Compensating with the
        // error already wiped, which would make it unrecoverable (RetryCompensationAsync only
        // accepts a Failed instance) and would also make ResumeIncompleteAsync pick it up forever.
        var instance = await store.GetAsync(corrupted.Id);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Failed);
        instance.Error.ShouldBe("original diagnostic");
    }

    [Fact(DisplayName = "Saga: ResumeIncompleteAsync resumes every other instance even when one fails to resume")]
    public async Task Saga_ResumeIncomplete_OneBadInstanceDoesNotBlockTheRest()
    {
        var store = new InMemorySagaStore();
        var goodSaga = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("Charge")]);
        var registry = new KyrolusSagaDefinitionRegistry([goodSaga]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);

        // A "bad" instance whose stored context cannot be deserialized.
        await store.SaveAsync(new KyrolusSagaInstance
        {
            SagaName = goodSaga.SagaName,
            ContextJson = "{not valid json",
            CurrentStepIndex = 0,
            Status = KyrolusSagaStatus.Running
        });

        // A perfectly good instance partway through.
        var goodContext = new TestSagaContext { Log = ["execute:Reserve"] };
        var goodInstance = new KyrolusSagaInstance
        {
            SagaName = goodSaga.SagaName,
            ContextJson = goodSaga.SerializeContext(goodContext),
            CurrentStepIndex = 1,
            Status = KyrolusSagaStatus.Running
        };
        await store.SaveAsync(goodInstance);

        var resumedCount = await coordinator.ResumeIncompleteAsync(CancellationToken.None);

        // Only the good one counts as resumed, but it must have actually run to completion rather
        // than being skipped because the bad one threw first.
        resumedCount.ShouldBe(1);
        var finished = await store.GetAsync(goodInstance.Id);
        finished.ShouldNotBeNull();
        finished.Status.ShouldBe(KyrolusSagaStatus.Completed);
    }

    [Fact(DisplayName = "InMemorySagaStore: a caller's own instance object mutated after SaveAsync does not affect the stored snapshot")]
    public async Task InMemorySagaStore_SaveAsync_StoresIndependentSnapshot()
    {
        var store = new InMemorySagaStore();
        var instance = new KyrolusSagaInstance
        {
            SagaName = "S",
            ContextJson = "{}",
            Status = KyrolusSagaStatus.Running,
            CurrentStepIndex = 0
        };

        await store.SaveAsync(instance);

        // Mutate the caller's own reference AFTER saving - simulates the coordinator continuing to
        // work on its local `instance` object between one SaveAsync call and the next.
        instance.CurrentStepIndex = 99;
        instance.Status = KyrolusSagaStatus.Failed;

        var stored = await store.GetAsync(instance.Id);
        stored.ShouldNotBeNull();
        stored.CurrentStepIndex.ShouldBe(0);
        stored.Status.ShouldBe(KyrolusSagaStatus.Running);
    }

    [Fact(DisplayName = "Saga registry: two definitions registered under the same name throw at construction")]
    public void SagaDefinitionRegistry_DuplicateName_Throws()
    {
        var sagaA = new TestSaga([new RecordingStep("A")]);
        var sagaB = new TestSaga([new RecordingStep("B")]); // same SagaName ("TestSaga") as sagaA

        Should.Throw<InvalidOperationException>(() => new KyrolusSagaDefinitionRegistry([sagaA, sagaB]));
    }

    [Fact(DisplayName = "AddKyrolusCqrsSaga wires the coordinator, registry and default in-memory store together")]
    public void AddKyrolusCqrsSaga_RegistersResolvableServices()
    {
        var services = new ServiceCollection();
        services.AddKyrolusCqrsSaga();
        services.AddKyrolusSaga<TestSaga>();
        services.AddSingleton<IReadOnlyList<IKyrolusSagaStep<TestSagaContext>>>([new RecordingStep("Only")]);

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IKyrolusSagaCoordinator>().ShouldNotBeNull();
        provider.GetRequiredService<IKyrolusSagaStore>().ShouldBeOfType<InMemorySagaStore>();
        provider.GetRequiredService<IKyrolusSagaDefinitionRegistry>().TryGet("TestSaga", out var definition).ShouldBeTrue();
        definition.ShouldBeOfType<TestSaga>();
    }
}
