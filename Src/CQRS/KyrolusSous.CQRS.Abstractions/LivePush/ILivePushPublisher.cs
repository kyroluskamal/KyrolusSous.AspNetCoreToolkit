namespace KyrolusSous.CQRS.Abstractions.LivePush;

/// <summary>
/// Defines a real-time push notification provider (e.g. SignalR Hub, WebSockets, or Server-Sent Events).
/// </summary>
public interface ILivePushPublisher
{
    /// <summary>
    /// Broadcasts data to the specified real-time channel or group.
    /// </summary>
    Task PublishLiveAsync(string channel, object? data, CancellationToken cancellationToken = default);
}
