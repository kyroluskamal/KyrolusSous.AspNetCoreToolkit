namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a command that broadcasts a real-time live notification upon successful completion.
/// </summary>
public interface IKyrolusLivePushCommand
{
    /// <summary>
    /// Gets the topic or channel to broadcast to (e.g. "orders", "chat-room-123").
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Gets the payload data to send to live subscribers. Defaults to this command instance if null.
    /// </summary>
    object? PushData => this;
}
