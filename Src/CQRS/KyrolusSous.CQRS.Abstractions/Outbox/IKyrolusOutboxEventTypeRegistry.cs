namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Resolves the CLR <see cref="Type"/> a stored outbox message's <see cref="KyrolusOutboxMessage.EventType"/>
/// name refers to, against an explicit allow-list rather than an open-ended search of every loaded type.
/// </summary>
/// <remarks>
/// <see cref="KyrolusOutboxProcessor"/> used to resolve <c>EventType</c> via <c>Type.GetType</c> and a
/// scan of every assembly in the current <see cref="AppDomain"/>, then deserialize the stored payload
/// straight into whatever type that name happened to name - any public type in any loaded assembly,
/// not only a notification the application actually publishes through the outbox. A registry narrows
/// that to types the application has explicitly declared publishable, so a message whose stored
/// <c>EventType</c> does not name one of them is rejected instead of instantiated.
/// </remarks>
public interface IKyrolusOutboxEventTypeRegistry
{
    /// <summary>Attempts to resolve <paramref name="eventTypeName"/> to an allow-listed type.</summary>
    bool TryResolve(string eventTypeName, out Type? eventType);
}
