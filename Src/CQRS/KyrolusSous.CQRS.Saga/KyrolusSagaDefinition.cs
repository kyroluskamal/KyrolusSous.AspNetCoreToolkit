using System.Text.Json;

namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Base class application code derives from to define a saga: its name and its ordered steps.
/// </summary>
/// <typeparam name="TContext">
/// The saga's shared state, carried through every step and persisted as JSON between steps so the
/// saga can be resumed after a crash. Keep it a plain, <see cref="System.Text.Json"/>-serializable
/// data holder (a record with simple properties) - not an EF entity, a live connection, or anything
/// else that cannot round-trip through JSON.
/// </typeparam>
/// <example>
/// <code>
/// public sealed record PlaceOrderContext
/// {
///     public required string OrderId { get; init; }
///     public required decimal Amount { get; init; }
///     public string? PaymentId { get; set; }
///     public string? ShipmentId { get; set; }
/// }
///
/// public sealed class PlaceOrderSaga : KyrolusSagaDefinition&lt;PlaceOrderContext&gt;
/// {
///     public override string SagaName =&gt; "PlaceOrder";
///
///     protected override IReadOnlyList&lt;IKyrolusSagaStep&lt;PlaceOrderContext&gt;&gt; Steps { get; } =
///     [
///         new ReserveStockStep(),
///         new ChargePaymentStep(),
///         new BookShipmentStep()
///     ];
/// }
///
/// // Registered once at startup:
/// services.AddKyrolusSaga&lt;PlaceOrderSaga&gt;();
///
/// // Started from a command handler:
/// await coordinator.StartAsync(sagaDefinition, new PlaceOrderContext { OrderId = id, Amount = total }, ct);
/// </code>
/// </example>
public abstract class KyrolusSagaDefinition<TContext> : IKyrolusSagaDefinition
{
    /// <inheritdoc cref="IKyrolusSagaDefinition.SagaName" />
    public abstract string SagaName { get; }

    /// <summary>The saga's steps, in the order they run.</summary>
    protected abstract IReadOnlyList<IKyrolusSagaStep<TContext>> Steps { get; }

    /// <inheritdoc />
    public int StepCount => Steps.Count;

    /// <inheritdoc />
    public string SerializeContext(object context) => JsonSerializer.Serialize((TContext)context);

    /// <inheritdoc />
    public object DeserializeContext(string json)
        => JsonSerializer.Deserialize<TContext>(json)
           ?? throw new InvalidOperationException($"[Kyrolus Saga] Deserializing the stored context for saga '{SagaName}' produced null.");

    /// <inheritdoc />
    public Task ExecuteStepAsync(int stepIndex, object context, CancellationToken cancellationToken)
        => Steps[stepIndex].ExecuteAsync((TContext)context, cancellationToken);

    /// <inheritdoc />
    public Task CompensateStepAsync(int stepIndex, object context, CancellationToken cancellationToken)
        => Steps[stepIndex].CompensateAsync((TContext)context, cancellationToken);
}
