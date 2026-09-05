using System.Text;
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
/// <remarks>
/// <typeparamref name="TContext"/> is constrained to <see langword="class"/> because
/// <see cref="KyrolusSagaCoordinator"/> holds the deserialized context as a single <see cref="object"/>
/// local across an entire run and re-serializes that same reference after every step - it never reads
/// a value back out of <see cref="ExecuteStepAsync"/> or <see cref="CompensateStepAsync"/>. That design
/// only works if a step's in-place mutation (<c>context.PaymentId = chargeId</c>) is visible through
/// every other reference to the same object, which requires reference semantics. If
/// <typeparamref name="TContext"/> were a value type, <c>(TContext)context</c> below would unbox into a
/// fresh stack copy on every call, so a step's mutation would land only on that ephemeral copy - the
/// coordinator's boxed <see cref="object"/> would never change, and the next
/// <c>SerializeContext(context)</c> would silently persist the stale, unmutated context. No exception
/// would be thrown; a later step, or a post-crash resume, would simply read back <c>null</c>/default
/// values for whatever the "successful" step appeared to have set. Making
/// <typeparamref name="TContext"/> a reference type turns that silent data loss into a compile error.
/// </remarks>
public abstract class KyrolusSagaDefinition<TContext> : IKyrolusSagaDefinition
    where TContext : class
{
    /// <inheritdoc cref="IKyrolusSagaDefinition.SagaName" />
    public abstract string SagaName { get; }

    /// <summary>The saga's steps, in the order they run.</summary>
    protected abstract IReadOnlyList<IKyrolusSagaStep<TContext>> Steps { get; }

    /// <inheritdoc />
    public int StepCount => Steps.Count;

    /// <inheritdoc />
    /// <remarks>
    /// Computed on every access rather than cached in a field, the same way <see cref="StepCount"/>
    /// is: <see cref="Steps"/> is an abstract property a derived class typically backs with its own
    /// field initializer (for example, a test <c>TestSaga</c> in the project's test suite), and a
    /// base-class field initializer for this property would run before that derived initializer does,
    /// reading <see cref="Steps"/> before it has a value.
    /// </remarks>
    public string StepSignature => ComputeStepSignature(Steps);

    /// <summary>
    /// FNV-1a (64-bit) over the UTF-8 bytes of every step's <see cref="IKyrolusSagaStep{TContext}.Name"/>,
    /// joined in order.
    /// </summary>
    /// <remarks>
    /// Not <see cref="string.GetHashCode()"/>: that hash is randomized per process specifically so it
    /// cannot be relied on across processes, which is exactly the one thing this value must do - be
    /// compared between the process that started a saga and a different process resuming it after a
    /// redeploy. FNV-1a needs no library and is more than collision-resistant enough for "did the step
    /// list change shape", which only ever needs to catch accidental drift, not withstand an adversary.
    /// </remarks>
    private static string ComputeStepSignature(IReadOnlyList<IKyrolusSagaStep<TContext>> steps)
    {
        const ulong FnvOffsetBasis = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        var hash = FnvOffsetBasis;
        for (var i = 0; i < steps.Count; i++)
        {
            if (i > 0) hash = MixByte(hash, (byte)'|');
            foreach (var b in Encoding.UTF8.GetBytes(steps[i].Name))
                hash = MixByte(hash, b);
        }

        return hash.ToString("x16");

        static ulong MixByte(ulong hash, byte b) => (hash ^ b) * FnvPrime;
    }

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
