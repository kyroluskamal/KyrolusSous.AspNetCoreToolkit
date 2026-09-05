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

    [Fact(DisplayName = "Saga: StartAsync called twice with the same correlation id returns the same instance and runs its steps only once")]
    public async Task Saga_StartAsync_SameCorrelationId_ReturnsSameInstance_RunsStepsOnce()
    {
        var store = new InMemorySagaStore();
        var registry = new KyrolusSagaDefinitionRegistry([]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);
        var reserveStep = new CountingStep("Reserve");
        var saga = new TestSaga([reserveStep]);

        var firstId = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None, correlationId: "corr-1");
        var secondId = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None, correlationId: "corr-1");

        secondId.ShouldBe(firstId);
        reserveStep.ExecuteCount.ShouldBe(1); // the second call must not have run the step again
        store.AllInstances.Count.ShouldBe(1); // and must not have created a second instance either

        var instance = await store.GetAsync(firstId);
        instance.ShouldNotBeNull();
        instance.CorrelationId.ShouldBe("corr-1");
    }

    [Fact(DisplayName = "Saga: StartAsync with different or no correlation ids still starts independent saga instances (regression)")]
    public async Task Saga_StartAsync_DifferentOrNoCorrelationId_StartsIndependentInstances()
    {
        var store = new InMemorySagaStore();
        var registry = new KyrolusSagaDefinitionRegistry([]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);
        var saga = new TestSaga([new RecordingStep("Reserve")]);

        var idA = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None, correlationId: "corr-a");
        var idB = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None, correlationId: "corr-b");
        var idC = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None); // no correlation id
        var idD = await coordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None); // no correlation id, again

        new[] { idA, idB, idC, idD }.Distinct().Count().ShouldBe(4);
        store.AllInstances.Count.ShouldBe(4);
    }

    [Fact(DisplayName = "InMemorySagaStore: GetByCorrelationIdAsync finds a stored instance by its correlation id, or returns null when none matches")]
    public async Task InMemorySagaStore_GetByCorrelationIdAsync_FindsByCorrelationIdOrReturnsNull()
    {
        var store = new InMemorySagaStore();
        var instance = new KyrolusSagaInstance { SagaName = "S", ContextJson = "{}", CorrelationId = "corr-x" };
        await store.SaveAsync(instance);

        var found = await store.GetByCorrelationIdAsync("corr-x");
        found.ShouldNotBeNull();
        found.Id.ShouldBe(instance.Id);

        (await store.GetByCorrelationIdAsync("does-not-exist")).ShouldBeNull();
    }

    [Fact(DisplayName = "Saga definition: StepSignature is stable for the same step names and changes when the step list's shape changes")]
    public void SagaDefinition_StepSignature_StableForSameShape_DiffersWhenShapeChanges()
    {
        var sameShapeA = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("Charge")]);
        var sameShapeB = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("Charge")]);
        var fewerSteps = new TestSaga([new RecordingStep("Reserve")]);
        var reordered = new TestSaga([new RecordingStep("Charge"), new RecordingStep("Reserve")]);
        var renamed = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("ChargeCard")]);

        sameShapeB.StepSignature.ShouldBe(sameShapeA.StepSignature);
        sameShapeA.StepCount.ShouldBe(sameShapeB.StepCount);

        fewerSteps.StepSignature.ShouldNotBe(sameShapeA.StepSignature);
        reordered.StepSignature.ShouldNotBe(sameShapeA.StepSignature);
        renamed.StepSignature.ShouldNotBe(sameShapeA.StepSignature);
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

    [Fact(DisplayName = "Saga: resuming after the step list's shape changed since this instance started fails clearly instead of silently completing")]
    public async Task Saga_Resume_StepShapeChanged_MarksFailedInsteadOfSilentlyCompleting()
    {
        var store = new InMemorySagaStore();

        // The instance started against this 3-step shape - StepCountAtStart/StepSignatureAtStart are
        // captured exactly as a real StartAsync call would, so the guard has something real to
        // compare against on resume.
        var originalSaga = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("Charge"), new RecordingStep("Ship")]);
        var crashedInstance = new KyrolusSagaInstance
        {
            SagaName = originalSaga.SagaName,
            ContextJson = originalSaga.SerializeContext(new TestSagaContext { Log = ["execute:Reserve"], ReservationId = "res-1" }),
            CurrentStepIndex = 1, // crashed after Reserve completed, before Charge ran
            Status = KyrolusSagaStatus.Running,
            StepCountAtStart = originalSaga.StepCount,
            StepSignatureAtStart = originalSaga.StepSignature
        };
        await store.SaveAsync(crashedInstance);

        // "Redeploy": the same saga name is now registered with only 2 steps - Ship was removed.
        // The unguarded bug's `for (stepIndex = 1; stepIndex < 2; ...)` would run Charge once and
        // then fall out of the loop, marking this Completed with no error, even though the original
        // saga's Ship step never ran and the instance never actually matched this shape.
        var redeployedSaga = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("Charge")]);
        var registry = new KyrolusSagaDefinitionRegistry([redeployedSaga]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);

        var resumedCount = await coordinator.ResumeIncompleteAsync(CancellationToken.None);

        resumedCount.ShouldBe(1); // reached a natural conclusion (Failed) rather than losing a race
        var instance = await store.GetAsync(crashedInstance.Id);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Failed);
        instance.Error.ShouldNotBeNull();
        instance.Error.ShouldContain("changed shape");
        instance.Error.ShouldContain("3 step(s)"); // expected shape
        instance.Error.ShouldContain("2 step(s)"); // current shape

        // Confirms it actually stopped, rather than being handed back to be retried forever.
        var incompleteAfter = await store.GetIncompleteAsync(CancellationToken.None);
        incompleteAfter.ShouldNotContain(i => i.Id == crashedInstance.Id);
    }

    [Fact(DisplayName = "Saga: resuming against its unchanged original definition still works exactly as before (regression)")]
    public async Task Saga_Resume_UnchangedDefinition_StillWorksAsBefore()
    {
        var store = new InMemorySagaStore();
        var saga = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("Charge"), new RecordingStep("Ship")]);
        var registry = new KyrolusSagaDefinitionRegistry([saga]);

        // Started for real (not hand-seeded) so StepCountAtStart/StepSignatureAtStart are recorded,
        // then crashed right after Reserve's completion is durably persisted - same technique as
        // Saga_RealStepMutation_SurvivesSimulatedCrashAndResumeWithFreshContextInstance.
        var crashingStore = new CrashAfterNthSaveStore(store, crashAfterSaveNumber: 2);
        var crashingCoordinator = new KyrolusSagaCoordinator(crashingStore, registry);
        await Should.ThrowAsync<InvalidOperationException>(
            () => crashingCoordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None));

        var crashed = store.AllInstances.Single();
        crashed.StepSignatureAtStart.ShouldNotBeNullOrEmpty(); // sanity: the guard has something to compare

        // Resumed against the SAME, unchanged saga definition - must complete exactly as it did
        // before this guard existed.
        var coordinator = new KyrolusSagaCoordinator(store, registry);
        var resumedCount = await coordinator.ResumeIncompleteAsync(CancellationToken.None);

        resumedCount.ShouldBe(1);
        var instance = await store.GetAsync(crashed.Id);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Completed);
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

    [Fact(DisplayName = "Saga: a corrupt context on the forward (Running) path is marked Failed instead of retried forever")]
    public async Task Saga_RunAsync_CorruptContext_MarksFailedInsteadOfStayingRunningForever()
    {
        var store = new InMemorySagaStore();
        var saga = new TestSaga([new RecordingStep("Reserve")]);
        var registry = new KyrolusSagaDefinitionRegistry([saga]);
        var coordinator = new KyrolusSagaCoordinator(store, registry);

        var corrupted = new KyrolusSagaInstance
        {
            SagaName = saga.SagaName,
            ContextJson = "{not valid json", // deliberately corrupt
            CurrentStepIndex = 0,
            Status = KyrolusSagaStatus.Running
        };
        await store.SaveAsync(corrupted);

        // First resume attempt: ResumeIncompleteAsync's own per-instance catch swallows the
        // exception (proven by Saga_ResumeIncomplete_OneBadInstanceDoesNotBlockTheRest), so the
        // only way to observe whether the instance got stuck is to check its persisted status
        // afterward - it must be Failed, not still Running.
        await coordinator.ResumeIncompleteAsync(CancellationToken.None);

        var instance = await store.GetAsync(corrupted.Id);
        instance.ShouldNotBeNull();
        instance.Status.ShouldBe(KyrolusSagaStatus.Failed);
        instance.Error.ShouldNotBeNull();

        // Confirms it actually stopped being retried: a Failed instance is no longer "incomplete",
        // so a second resume pass must not pick it up again.
        var incompleteAfter = await store.GetIncompleteAsync(CancellationToken.None);
        incompleteAfter.ShouldNotContain(i => i.Id == corrupted.Id);
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

    /// <summary>
    /// Decorates a store so that the Nth call to <see cref="SaveAsync"/> is applied to
    /// <paramref name="inner"/> - the write durably lands - and only then throws, simulating a process
    /// that crashes in the instant right after a step's progress was persisted but before the
    /// coordinator's loop moves on to run the next step.
    /// </summary>
    private sealed class CrashAfterNthSaveStore(IKyrolusSagaStore inner, int crashAfterSaveNumber) : IKyrolusSagaStore
    {
        private int _saveCount;

        public Task<KyrolusSagaInstance?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
            => inner.GetAsync(sagaId, cancellationToken);

        public Task<IReadOnlyList<KyrolusSagaInstance>> GetIncompleteAsync(CancellationToken cancellationToken = default)
            => inner.GetIncompleteAsync(cancellationToken);

        public Task<KyrolusSagaInstance?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
            => inner.GetByCorrelationIdAsync(correlationId, cancellationToken);

        public async Task<bool> SaveAsync(KyrolusSagaInstance instance, CancellationToken cancellationToken = default)
        {
            var result = await inner.SaveAsync(instance, cancellationToken);
            if (Interlocked.Increment(ref _saveCount) == crashAfterSaveNumber)
                throw new InvalidOperationException("Simulated process crash right after this save was durably persisted.");
            return result;
        }
    }

    [Fact(DisplayName = "Saga: a step's real in-place mutation of a reference-type context is still visible after a simulated crash, read back by a brand-new coordinator and context instance")]
    public async Task Saga_RealStepMutation_SurvivesSimulatedCrashAndResumeWithFreshContextInstance()
    {
        var innerStore = new InMemorySagaStore();
        var reserveStep = new RecordingStep("Reserve"); // sets context.ReservationId = "res-1"
        var chargeStep = new RecordingStep("Charge");
        var saga = new TestSaga([reserveStep, chargeStep]);
        var registry = new KyrolusSagaDefinitionRegistry([saga]);

        // Save #1 is StartAsync's initial persist (Running, step 0); save #2 is the persist right
        // after Reserve completes (step 1, ReservationId mutated in). The store throws right after
        // that second save lands in innerStore, before the coordinator's loop ever starts Charge.
        var crashingStore = new CrashAfterNthSaveStore(innerStore, crashAfterSaveNumber: 2);
        var crashingCoordinator = new KyrolusSagaCoordinator(crashingStore, registry);

        await Should.ThrowAsync<InvalidOperationException>(
            () => crashingCoordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None));

        var afterCrash = innerStore.AllInstances.Single();
        afterCrash.Status.ShouldBe(KyrolusSagaStatus.Running);
        afterCrash.CurrentStepIndex.ShouldBe(1);

        // The mutation Reserve made is durably persisted even though the process "crashed" right
        // after - this is exactly what a value-type TContext could not guarantee (see the
        // <see langword="class"/> constraint's <c>&lt;remarks&gt;</c> on KyrolusSagaDefinition).
        var persistedContext = (TestSagaContext)saga.DeserializeContext(afterCrash.ContextJson);
        persistedContext.ReservationId.ShouldBe("res-1");
        persistedContext.Log.ShouldBe(["execute:Reserve"]);

        // A brand-new coordinator resumes it, deserializing a brand-new TestSagaContext instance -
        // nothing from the crashed run's object graph is reused.
        var freshCoordinator = new KyrolusSagaCoordinator(innerStore, registry);
        var resumedCount = await freshCoordinator.ResumeIncompleteAsync(CancellationToken.None);

        resumedCount.ShouldBe(1);
        var finalInstance = await innerStore.GetAsync(afterCrash.Id);
        finalInstance.ShouldNotBeNull();
        finalInstance.Status.ShouldBe(KyrolusSagaStatus.Completed);

        var finalContext = (TestSagaContext)saga.DeserializeContext(finalInstance.ContextJson);
        // Reserve was NOT re-run (exactly one "execute:Reserve"), and its ReservationId mutation -
        // made in a run whose process no longer exists by the time this coordinator resumes it - is
        // still there.
        finalContext.Log.ShouldBe(["execute:Reserve", "execute:Charge"]);
        finalContext.ReservationId.ShouldBe("res-1");
    }

    /// <summary>
    /// Decorates a store so that every caller of <see cref="GetAsync"/> (or every caller of
    /// <see cref="GetIncompleteAsync"/>) blocks until a second concurrent caller arrives at the same
    /// method, then releases both together. Used to force the exact race the version check has to
    /// survive - two callers reading the same version before either writes - deterministically,
    /// instead of hoping two Tasks happen to interleave that way under real timing.
    /// </summary>
    private sealed class RendezvousReadStore(IKyrolusSagaStore inner) : IKyrolusSagaStore
    {
        private readonly SemaphoreSlim _gate = new(0, 2);
        private int _arrived;

        public async Task<KyrolusSagaInstance?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
        {
            var result = await inner.GetAsync(sagaId, cancellationToken);
            await RendezvousAsync();
            return result;
        }

        public async Task<IReadOnlyList<KyrolusSagaInstance>> GetIncompleteAsync(CancellationToken cancellationToken = default)
        {
            var result = await inner.GetIncompleteAsync(cancellationToken);
            await RendezvousAsync();
            return result;
        }

        public Task<KyrolusSagaInstance?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
            => inner.GetByCorrelationIdAsync(correlationId, cancellationToken);

        public Task<bool> SaveAsync(KyrolusSagaInstance instance, CancellationToken cancellationToken = default)
            => inner.SaveAsync(instance, cancellationToken);

        private async Task RendezvousAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= 2)
                _gate.Release(2);
            else
                await _gate.WaitAsync();
        }
    }

    private sealed class CountingStep(string name) : IKyrolusSagaStep<TestSagaContext>
    {
        public string Name => name;
        public int ExecuteCount;

        public Task ExecuteAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ExecuteCount);
            context.Log.Add($"execute:{name}");
            return Task.CompletedTask;
        }

        public Task CompensateAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            context.Log.Add($"compensate:{name}");
            return Task.CompletedTask;
        }
    }

    private sealed class CountingFlakyCompensateStep(string name, Func<bool> shouldFail) : IKyrolusSagaStep<TestSagaContext>
    {
        public string Name => name;
        public int CompensateCount;

        public Task ExecuteAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            context.Log.Add($"execute:{name}");
            return Task.CompletedTask;
        }

        public Task CompensateAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            if (shouldFail()) throw new InvalidOperationException($"{name} compensate failed");
            Interlocked.Increment(ref CompensateCount);
            context.Log.Add($"compensate:{name}");
            return Task.CompletedTask;
        }
    }

    [Fact(DisplayName = "Saga: two concurrent RetryCompensationAsync calls for the same failed instance compensate exactly once, not twice")]
    public async Task Saga_RetryCompensation_ConcurrentCalls_CompensatesExactlyOnce()
    {
        var innerStore = new InMemorySagaStore();
        var chargeShouldFail = true;
        var chargeStep = new CountingFlakyCompensateStep("Charge", () => chargeShouldFail);
        var saga = new TestSaga([new RecordingStep("Reserve"), chargeStep, new RecordingStep("Ship", failExecute: true)]);
        var registry = new KyrolusSagaDefinitionRegistry([saga]);

        // Set up an instance stuck Failed (Charge's compensation failed) via an ordinary, uncontended
        // run - only the retry below needs to race.
        var setupCoordinator = new KyrolusSagaCoordinator(innerStore, registry);
        var sagaId = await setupCoordinator.StartAsync(saga, new TestSagaContext(), CancellationToken.None);
        (await innerStore.GetAsync(sagaId))!.Status.ShouldBe(KyrolusSagaStatus.Failed);

        chargeShouldFail = false;

        var racingStore = new RendezvousReadStore(innerStore);
        var coordinator = new KyrolusSagaCoordinator(racingStore, registry);

        // A double-click on "retry", or two app instances both processing the same failed saga: both
        // read Status == Failed at the same version before either can flip it to Compensating.
        var callA = coordinator.RetryCompensationAsync(sagaId, CancellationToken.None);
        var callB = coordinator.RetryCompensationAsync(sagaId, CancellationToken.None);
        await Task.WhenAll(callA, callB);

        // The unguarded bug would run Charge's compensation (e.g. a refund) twice in parallel.
        chargeStep.CompensateCount.ShouldBe(1);

        var final = await innerStore.GetAsync(sagaId);
        final.ShouldNotBeNull();
        final.Status.ShouldBe(KyrolusSagaStatus.Compensated);
    }

    [Fact(DisplayName = "Saga: two concurrent ResumeIncompleteAsync calls resume the same instance's next step exactly once, not twice")]
    public async Task Saga_ResumeIncomplete_ConcurrentCalls_ExecutesNextStepExactlyOnce()
    {
        var innerStore = new InMemorySagaStore();
        var reserveStep = new RecordingStep("Reserve");
        var chargeStep = new CountingStep("Charge");
        var shipStep = new CountingStep("Ship");
        var saga = new TestSaga([reserveStep, chargeStep, shipStep]);
        var registry = new KyrolusSagaDefinitionRegistry([saga]);

        // Seed an instance left Running after Reserve, as if a process crashed right after it - same
        // setup as Saga_ResumeIncomplete_ContinuesFromPersistedStep, just without going through
        // StartAsync (which would also execute Charge/Ship on the setup coordinator).
        var context = new TestSagaContext { Log = ["execute:Reserve"] };
        var crashedInstance = new KyrolusSagaInstance
        {
            SagaName = saga.SagaName,
            ContextJson = saga.SerializeContext(context),
            CurrentStepIndex = 1,
            Status = KyrolusSagaStatus.Running
        };
        await innerStore.SaveAsync(crashedInstance);

        var racingStore = new RendezvousReadStore(innerStore);
        var coordinator = new KyrolusSagaCoordinator(racingStore, registry);

        // Two overlapping resume passes (two app instances, or an overlapping timer tick) both fetch
        // the same incomplete instance at the same version before either can claim it.
        var resumeA = coordinator.ResumeIncompleteAsync(CancellationToken.None);
        var resumeB = coordinator.ResumeIncompleteAsync(CancellationToken.None);
        var results = await Task.WhenAll(resumeA, resumeB);

        // The unguarded bug would execute Charge (and then Ship) a second time in the losing call.
        chargeStep.ExecuteCount.ShouldBe(1);
        shipStep.ExecuteCount.ShouldBe(1);
        results.Sum().ShouldBe(1); // only the winning call counts the instance as resumed

        var final = await innerStore.GetAsync(crashedInstance.Id);
        final.ShouldNotBeNull();
        final.Status.ShouldBe(KyrolusSagaStatus.Completed);
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

    [Fact(DisplayName = "InMemorySagaStore: SaveAsync rejects a write whose Version is behind the stored one, and leaves the stored row untouched")]
    public async Task InMemorySagaStore_SaveAsync_RejectsStaleVersion()
    {
        var store = new InMemorySagaStore();
        var instance = new KyrolusSagaInstance { SagaName = "S", ContextJson = "{}" };

        (await store.SaveAsync(instance)).ShouldBeTrue(); // Version 0 -> 1, instance.Version updated in place
        instance.Version.ShouldBe(1);

        // A second, independent read of the same row - its own copy at the version that was current
        // before the first caller's write landed.
        var staleReader = await store.GetAsync(instance.Id);
        staleReader.ShouldNotBeNull();

        instance.CurrentStepIndex = 1;
        (await store.SaveAsync(instance)).ShouldBeTrue(); // Version 1 -> 2, still the only writer so far

        staleReader.CurrentStepIndex = 99;
        (await store.SaveAsync(staleReader)).ShouldBeFalse(); // still carries Version 1; store is at 2

        var current = await store.GetAsync(instance.Id);
        current.ShouldNotBeNull();
        current.Version.ShouldBe(2);
        current.CurrentStepIndex.ShouldBe(1); // the rejected write's CurrentStepIndex = 99 never applied
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
