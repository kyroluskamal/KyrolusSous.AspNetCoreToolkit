namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Resolves a <see cref="KyrolusSagaInstance.SagaName"/> back to the <see cref="IKyrolusSagaDefinition"/>
/// that can run it, so <see cref="IKyrolusSagaCoordinator.ResumeIncompleteAsync"/> can find the right
/// definition for an instance loaded from storage without knowing its <c>TContext</c> type ahead of
/// time.
/// </summary>
/// <remarks>
/// Built from definitions registered explicitly via DI (<c>AddKyrolusSaga&lt;TSagaDefinition&gt;()</c>)
/// - an allow-list, the same shape as <c>IKyrolusOutboxEventTypeRegistry</c> in
/// <c>KyrolusSous.CQRS.Abstractions</c> and for the same reason: resuming a saga from a stored name
/// should only ever be able to reach a definition the application actually registered, never resolve
/// an arbitrary type by name.
/// </remarks>
public interface IKyrolusSagaDefinitionRegistry
{
    /// <summary>Attempts to find the registered definition for <paramref name="sagaName"/>.</summary>
    bool TryGet(string sagaName, out IKyrolusSagaDefinition? definition);
}
