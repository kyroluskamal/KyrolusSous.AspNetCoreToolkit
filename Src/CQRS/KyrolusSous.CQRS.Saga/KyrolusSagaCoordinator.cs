using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Default <see cref="IKyrolusSagaCoordinator"/>.
/// </summary>
/// <remarks>
/// Progress is persisted after every single step - forward or compensating - before the next one
/// starts, specifically so a crash between two steps loses nothing: <see cref="ResumeIncompleteAsync"/>
/// re-reads <see cref="KyrolusSagaInstance.CurrentStepIndex"/> and continues from exactly there,
/// never re-running a step that already completed and never skipping one that did not.
/// </remarks>
public sealed class KyrolusSagaCoordinator(
    IKyrolusSagaStore store,
    IKyrolusSagaDefinitionRegistry registry,
    ILogger<KyrolusSagaCoordinator>? logger = null) : IKyrolusSagaCoordinator
{
    private readonly IKyrolusSagaStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IKyrolusSagaDefinitionRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ILogger? _logger = logger;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// When <paramref name="correlationId"/> is supplied, this looks for an existing instance first
    /// and, if one is found, returns its id directly without starting a new saga or re-running any
    /// step - the same "claim once, return the same result on retry" philosophy
    /// <c>KyrolusIdempotencyBehavior&lt;TRequest, TResponse&gt;</c> applies to
    /// commands, just applied to starting a saga instead.
    /// </para>
    /// <para>
    /// <b>The race this does NOT close:</b> two concurrent calls that both pass the SAME brand-new
    /// <paramref name="correlationId"/> - one neither call's lookup has seen yet - can both miss
    /// <see cref="IKyrolusSagaStore.GetByCorrelationIdAsync"/> and both proceed to create their own
    /// instance. <see cref="TrySaveAsync"/>'s optimistic-concurrency check cannot catch this the way it
    /// catches every other race in this class: that check only rejects a second writer of the SAME
    /// <see cref="KyrolusSagaInstance.Id"/> that read the same <see cref="KyrolusSagaInstance.Version"/>,
    /// and here each caller creates its OWN new id with <see cref="KyrolusSagaInstance.Version"/> 0 -
    /// nothing about the two writes collides at the storage layer, so both succeed and two independent
    /// sagas run. Closing this completely would need the store to expose an atomic "claim this
    /// correlation id" primitive ahead of the general-purpose <see cref="IKyrolusSagaStore.SaveAsync"/>
    /// path - a unique constraint in a database-backed store, say - which is a bigger change to
    /// <see cref="IKyrolusSagaStore"/>'s contract than this purely additive feature takes on, and one
    /// every existing store implementation would need to adopt to actually benefit from it. The lookup
    /// is therefore done as late as possible, with nothing but object construction between it and the
    /// first save, which narrows the window to one store round trip rather than eliminating it. A
    /// caller for whom even that residual window is unacceptable should claim the correlation id one
    /// layer up first (e.g. through the idempotency behavior referenced above, keyed on the same id).
    /// </para>
    /// </remarks>
    public async Task<Guid> StartAsync<TContext>(
        KyrolusSagaDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default,
        string? correlationId = null)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var existing = await _store.GetByCorrelationIdAsync(correlationId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                _logger?.LogInformation(
                    "[Kyrolus Saga] StartAsync called again with correlation id '{CorrelationId}'; returning the " +
                    "existing saga instance {SagaId} instead of starting a second one.",
                    correlationId,
                    existing.Id);
                return existing.Id;
            }
        }

        var instance = new KyrolusSagaInstance
        {
            SagaName = definition.SagaName,
            ContextJson = definition.SerializeContext(context!),
            CurrentStepIndex = 0,
            Status = KyrolusSagaStatus.Running,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId,
            StepCountAtStart = definition.StepCount,
            StepSignatureAtStart = definition.StepSignature
        };

        // A brand new id cannot conceivably lose the version race below (nothing else has ever
        // written it), so the result needs no handling beyond persisting the initial state.
        await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false);
        await RunAsync(definition, instance, cancellationToken).ConfigureAwait(false);
        return instance.Id;
    }

    /// <inheritdoc />
    public async Task<int> ResumeIncompleteAsync(CancellationToken cancellationToken = default)
    {
        var incomplete = await _store.GetIncompleteAsync(cancellationToken).ConfigureAwait(false);
        var resumed = 0;

        foreach (var instance in incomplete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_registry.TryGet(instance.SagaName, out var definition) || definition is null)
            {
                _logger?.LogWarning(
                    "[Kyrolus Saga] No definition registered for saga '{SagaName}' (instance {SagaId}); cannot resume it.",
                    instance.SagaName,
                    instance.Id);
                continue;
            }

            try
            {
                // Claims this instance before touching it: two concurrent ResumeIncompleteAsync calls
                // (two app instances, or an overlapping timer tick) can both read the same incomplete
                // instance from GetIncompleteAsync above before either writes. Only one of them can win
                // this version-checked save; the loser must stop here, before RunAsync ever runs a step's
                // side-effecting action a second time for it - re-executing it after the fact would be
                // too late, the step would already have run twice.
                if (!await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false))
                    continue;

                if (await RunAsync(definition, instance, cancellationToken).ConfigureAwait(false))
                    resumed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller asked the whole resume operation to stop - propagate rather than
                // treating it as "this one instance failed", which the next branch would otherwise do.
                throw;
            }
            catch (Exception ex)
            {
                // One instance with a corrupt ContextJson, an unhandled step exception, or any other
                // failure must not abort every OTHER incomplete instance still waiting to resume -
                // each is independent. Logged and skipped; it stays incomplete and is picked up again
                // on the next ResumeIncompleteAsync call once whatever is wrong with it is fixed.
                _logger?.LogError(
                    ex,
                    "[Kyrolus Saga] Failed to resume saga '{SagaName}' (instance {SagaId}); leaving it as-is and continuing with the rest.",
                    instance.SagaName,
                    instance.Id);
            }
        }

        return resumed;
    }

    /// <inheritdoc />
    public async Task RetryCompensationAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        var instance = await _store.GetAsync(sagaId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"[Kyrolus Saga] No saga instance found with id '{sagaId}'.");

        if (instance.Status != KyrolusSagaStatus.Failed)
            throw new InvalidOperationException(
                $"[Kyrolus Saga] Instance '{sagaId}' is {instance.Status}, not Failed - only a failed compensation can be retried.");

        if (!_registry.TryGet(instance.SagaName, out var definition) || definition is null)
            throw new InvalidOperationException($"[Kyrolus Saga] No definition registered for saga '{instance.SagaName}'.");

        // Deserialize BEFORE flipping status to Compensating and persisting that: if the stored
        // context is corrupt (bad JSON, TContext's shape changed since this row was written), this
        // must fail with the instance still Failed and its original Error message intact - not left
        // stuck as Compensating with the diagnostic already erased and no way back into Failed to
        // retry again, which is what flipping the status first would do.
        var context = definition.DeserializeContext(instance.ContextJson);

        instance.Status = KyrolusSagaStatus.Compensating;
        instance.Error = null;

        // This is the claim: two concurrent RetryCompensationAsync calls for the same sagaId can both
        // read Status == Failed above before either writes. Only one of them can win this version-checked
        // save; the loser must stop here rather than proceed into CompensateAsync - otherwise both would
        // run the same compensating step (e.g. the same refund) in parallel.
        if (!await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false))
            return;

        await CompensateAsync(definition, instance, context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves through the store's version check and, on a lost race, logs and reports it rather than
    /// throwing - losing this race means another caller already claimed <paramref name="instance"/>
    /// and is handling it, which is an expected outcome under concurrent access, not an error.
    /// </summary>
    private async Task<bool> TrySaveAsync(KyrolusSagaInstance instance, CancellationToken cancellationToken)
    {
        if (await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false))
            return true;

        _logger?.LogInformation(
            "[Kyrolus Saga] Lost a concurrent write race for saga '{SagaName}' (instance {SagaId}); " +
            "another caller already advanced it past the version this call read - treating it as already handled and skipping.",
            instance.SagaName,
            instance.Id);
        return false;
    }

    /// <returns>
    /// <see langword="false"/> if this call lost a version race partway through and stopped instead of
    /// running to a natural conclusion (a terminal status, or a business failure recorded as such);
    /// <see langword="true"/> otherwise.
    /// </returns>
    private async Task<bool> RunAsync(IKyrolusSagaDefinition definition, KyrolusSagaInstance instance, CancellationToken cancellationToken)
    {
        object context;
        try
        {
            context = definition.DeserializeContext(instance.ContextJson);
        }
        catch (Exception ex)
        {
            // Left uncaught, this would leave the instance stuck as Running (or Compensating)
            // forever: ResumeIncompleteAsync's own catch logs the failure but does not change the
            // instance's status, so GetIncompleteAsync keeps handing it back on every future call,
            // and it can never reach Failed - the only status RetryCompensationAsync accepts - so
            // there is no way to rescue it either. Marking it Failed here, the same way a failed
            // compensation step already does, at least makes it stop being silently retried forever
            // and turns it into something discoverable that needs a human (a schema-drifted TContext,
            // corrupt JSON, or similar) rather than an invisible zombie.
            //
            // The save's outcome is not checked: this throws either way, since the deserialize
            // failure is the real problem here - a lost race just means someone else's write is left
            // standing instead of being overwritten with this diagnostic, which is fine.
            instance.Status = KyrolusSagaStatus.Failed;
            instance.Error = $"Failed to deserialize stored context: {ex.Message}";
            await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false);
            throw;
        }

        // Guards against resuming a crashed/compensating instance against a step list that no
        // longer has the shape it started against - see the <remarks> on
        // KyrolusSagaInstance.StepSignatureAtStart for the exact failure this prevents (a shorter
        // step list silently marking a saga Completed or Compensated without ever running or
        // compensating the steps that no longer exist at their old indices). Skipped when
        // StepSignatureAtStart is null: that means this instance predates the guard, or was built
        // directly without setting it (as several existing tests do), and there is nothing to compare
        // it against - treating "nothing recorded" as itself a mismatch would fail every saga already
        // in flight the moment this guard shipped, which is exactly what the feature's "purely
        // additive" requirement rules out. A freshly created instance always matches here: StartAsync
        // captures both values from the same definition instance it immediately hands to RunAsync.
        if (instance.StepSignatureAtStart is not null &&
            (instance.StepCountAtStart != definition.StepCount || instance.StepSignatureAtStart != definition.StepSignature))
        {
            _logger?.LogError(
                "[Kyrolus Saga] Saga '{SagaName}' (instance {SagaId}) started against {ExpectedStepCount} step(s) " +
                "(signature '{ExpectedSignature}') but the current definition now has {ActualStepCount} step(s) " +
                "(signature '{ActualSignature}'). Resuming would re-apply the persisted step index against a step " +
                "list this instance never actually ran against, so it is being marked Failed instead of guessing.",
                instance.SagaName,
                instance.Id,
                instance.StepCountAtStart,
                instance.StepSignatureAtStart,
                definition.StepCount,
                definition.StepSignature);

            instance.Status = KyrolusSagaStatus.Failed;
            instance.Error = $"Saga step list changed shape since this instance started: expected " +
                $"{instance.StepCountAtStart} step(s) (signature '{instance.StepSignatureAtStart}'), but the current " +
                $"definition '{definition.SagaName}' has {definition.StepCount} step(s) (signature " +
                $"'{definition.StepSignature}'). Resuming against a different step shape than the one this instance " +
                "started against could re-run, skip, or misattribute steps, so this was refused rather than guessed.";
            return await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false);
        }

        if (instance.Status == KyrolusSagaStatus.Compensating)
            return await CompensateAsync(definition, instance, context, cancellationToken).ConfigureAwait(false);

        for (var stepIndex = instance.CurrentStepIndex; stepIndex < definition.StepCount; stepIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await definition.ExecuteStepAsync(stepIndex, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is not a business failure - a step honoring the token it was given is
                // not "wrong" and must not be undone as if it were. Propagate so the caller sees the
                // cancellation, with the instance left exactly where the loop's own SaveAsync calls
                // already put it (the last successfully completed step, still Running).
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "[Kyrolus Saga] Step {StepIndex} of saga '{SagaName}' (instance {SagaId}) failed; compensating completed steps.",
                    stepIndex,
                    instance.SagaName,
                    instance.Id);

                // stepIndex itself never completed, so only [0..stepIndex-1] need undoing - recorded
                // as "one past the last step to compensate", matching CompensateAsync's convention.
                instance.Status = KyrolusSagaStatus.Compensating;
                instance.CurrentStepIndex = stepIndex;
                instance.Error = ex.Message;
                instance.ContextJson = definition.SerializeContext(context);
                if (!await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false))
                    return false;

                return await CompensateAsync(definition, instance, context, cancellationToken).ConfigureAwait(false);
            }

            // Re-serialized after every step, not only on failure: a step routinely writes into the
            // context for a later step to read (an order id, a payment id), and a crash between two
            // steps must not lose that write - resuming has to see it too.
            instance.CurrentStepIndex = stepIndex + 1;
            instance.ContextJson = definition.SerializeContext(context);
            if (!await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false))
                return false;
        }

        instance.Status = KyrolusSagaStatus.Completed;
        instance.CompletedAtUtc = DateTimeOffset.UtcNow;
        return await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false);
    }

    /// <returns>
    /// <see langword="false"/> if this call lost a version race partway through and stopped instead of
    /// running to a natural conclusion (Compensated, or Failed with the compensation error recorded);
    /// <see langword="true"/> otherwise.
    /// </returns>
    private async Task<bool> CompensateAsync(
        IKyrolusSagaDefinition definition,
        KyrolusSagaInstance instance,
        object context,
        CancellationToken cancellationToken)
    {
        // CurrentStepIndex is one past the last step that still needs undoing, so this walks
        // backward from index - 1 down to 0, persisting after every step so a crash mid-compensation
        // resumes from exactly the step it was on rather than re-undoing an already-undone step.
        for (var stepIndex = instance.CurrentStepIndex - 1; stepIndex >= 0; stepIndex--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await definition.CompensateStepAsync(stepIndex, context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Same reasoning as the forward loop: cancellation is not "this compensation step
                // failed" - propagate, leaving the instance exactly where the loop's own SaveAsync
                // calls already put it.
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "[Kyrolus Saga] Compensating step {StepIndex} of saga '{SagaName}' (instance {SagaId}) failed. " +
                    "Manual intervention required - call RetryCompensationAsync once the cause is resolved.",
                    stepIndex,
                    instance.SagaName,
                    instance.Id);

                instance.Status = KyrolusSagaStatus.Failed;
                instance.CurrentStepIndex = stepIndex + 1; // steps [0..stepIndex] still need compensating
                instance.Error = $"Compensation failed at step {stepIndex}: {ex.Message}";
                instance.ContextJson = definition.SerializeContext(context);
                return await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false);
            }

            instance.CurrentStepIndex = stepIndex; // steps [0..stepIndex-1] still need compensating
            instance.ContextJson = definition.SerializeContext(context);
            if (!await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false))
                return false;
        }

        instance.Status = KyrolusSagaStatus.Compensated;
        instance.CompletedAtUtc = DateTimeOffset.UtcNow;
        return await TrySaveAsync(instance, cancellationToken).ConfigureAwait(false);
    }
}
