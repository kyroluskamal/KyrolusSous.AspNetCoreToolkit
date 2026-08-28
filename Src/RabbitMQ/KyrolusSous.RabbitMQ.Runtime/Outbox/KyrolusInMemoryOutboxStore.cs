using System.Collections.Concurrent;
using KyrolusSous.RabbitMQ.Abstractions.Outbox;

namespace KyrolusSous.RabbitMQ.Runtime.Outbox;

/// <summary>
/// Thread-safe in-memory store for transactional outbox messages.
/// </summary>
public class KyrolusInMemoryOutboxStore : IKyrolusOutboxStore
{
    private readonly ConcurrentDictionary<string, IKyrolusOutboxMessage> _messages = new();

    public Task AddAsync(IKyrolusOutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IKyrolusOutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var pending = _messages.Values
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<IKyrolusOutboxMessage>>(pending);
    }

    public Task MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.ProcessedAt = DateTimeOffset.UtcNow;
            msg.Error = null;
        }

        return Task.CompletedTask;
    }

    public Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.RetryCount++;
            msg.Error = error;
        }

        return Task.CompletedTask;
    }
}
