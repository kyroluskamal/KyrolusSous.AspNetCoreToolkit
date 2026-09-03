namespace KyrolusSous.CQRS.Abstractions.LivePush;

/// <summary>
/// Default live push publisher that logs real-time broadcast events.
/// </summary>
public sealed class LoggerLivePushPublisher(ILogger<LoggerLivePushPublisher> logger) : ILivePushPublisher
{
    private readonly ILogger _logger = logger;

    public Task PublishLiveAsync(string channel, object? data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Kyrolus CQRS LivePush] Broadcast to channel '{Channel}': {@Data}", channel, data);
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory live push publisher for testing and verification.
/// </summary>
public sealed class InMemoryLivePushPublisher : ILivePushPublisher
{
    private readonly ConcurrentBag<(string Channel, object? Data)> _messages = [];

    public IReadOnlyCollection<(string Channel, object? Data)> Messages => _messages.ToArray();

    public Task PublishLiveAsync(string channel, object? data, CancellationToken cancellationToken = default)
    {
        _messages.Add((channel, data));
        return Task.CompletedTask;
    }

    public void Clear() => _messages.Clear();
}
