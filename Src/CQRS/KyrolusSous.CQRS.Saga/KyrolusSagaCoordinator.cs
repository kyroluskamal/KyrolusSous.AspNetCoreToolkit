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
    public async Task<Guid> StartAsync<TContext>(
        KyrolusSagaDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var instance = new KyrolusSagaInstance
        {
            SagaName = definition.SagaName,
            ContextJson = definition.SerializeContext(context!),
            CurrentStepIndex = 0,
            Status = KyrolusSagaStatus.Running
        };

        await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
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
                await RunAsync(definition, instance, cancellationToken).ConfigureAwait(false);
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
        await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false);

        await CompensateAsync(definition, instance, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(IKyrolusSagaDefinition definition, KyrolusSagaInstance instance, CancellationToken cancellationToken)
    {
        var context = definition.DeserializeContext(instance.ContextJson);

        if (instance.Status == KyrolusSagaStatus.Compensating)
        {
            await CompensateAsync(definition, instance, context, cancellationToken).ConfigureAwait(false);
            return;
        }

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
                await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false);

                await CompensateAsync(definition, instance, context, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Re-serialized after every step, not only on failure: a step routinely writes into the
            // context for a later step to read (an order id, a payment id), and a crash between two
            // steps must not lose that write - resuming has to see it too.
            instance.CurrentStepIndex = stepIndex + 1;
            instance.ContextJson = definition.SerializeContext(context);
            await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
        }

        instance.Status = KyrolusSagaStatus.Completed;
        instance.CompletedAtUtc = DateTimeOffset.UtcNow;
        await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
    }

    private async Task CompensateAsync(
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
                await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
                return;
            }

            instance.CurrentStepIndex = stepIndex; // steps [0..stepIndex-1] still need compensating
            instance.ContextJson = definition.SerializeContext(context);
            await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
        }

        instance.Status = KyrolusSagaStatus.Compensated;
        instance.CompletedAtUtc = DateTimeOffset.UtcNow;
        await _store.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
    }
}
