namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// One step of a saga: a forward action, and the compensating (undo) action that reverses it if a
/// later step in the same saga fails.
/// </summary>
/// <typeparam name="TContext">
/// The saga's shared state. Every step reads and writes the same instance, so this is where a step
/// hands data to the ones after it (an order id created in step 1, needed to cancel it in step 3's
/// compensation).
/// </typeparam>
/// <remarks>
/// A saga coordinates a business process that spans more than one system or more than one
/// transaction - "reserve stock, charge the card, book the shipment" - where no single database
/// transaction can cover all three. If step 3 fails, there is no rollback to fall back on: the only
/// way to undo "the card was charged" is to run code that refunds it. <see cref="ExecuteAsync"/> is
/// that forward action; <see cref="CompensateAsync"/> is its undo, and the coordinator only ever calls
/// it for a step whose <see cref="ExecuteAsync"/> already completed.
/// <para>
/// <typeparamref name="TContext"/> is constrained to <see langword="class"/> for the same reason
/// <see cref="KyrolusSagaDefinition{TContext}"/> is: a step is only ever driven by
/// <see cref="KyrolusSagaCoordinator"/>, which passes the one context instance through every step by
/// reference and expects a step's in-place write to be visible to the step after it, and to whatever
/// gets serialized once the step returns. A value-type <typeparamref name="TContext"/> would receive a
/// stack copy in <see cref="ExecuteAsync"/>, so any mutation would vanish the moment the method
/// returns - silently, with no exception.
/// </para>
/// </remarks>
public interface IKyrolusSagaStep<TContext>
    where TContext : class
{
    /// <summary>A short, stable name for this step - used in logs and diagnostics.</summary>
    string Name { get; }

    /// <summary>Runs the step's forward action.</summary>
    Task ExecuteAsync(TContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Undoes this step's forward action. Only ever called for a step whose <see cref="ExecuteAsync"/>
    /// already completed successfully, when a later step in the same saga failed.
    /// </summary>
    Task CompensateAsync(TContext context, CancellationToken cancellationToken);
}
