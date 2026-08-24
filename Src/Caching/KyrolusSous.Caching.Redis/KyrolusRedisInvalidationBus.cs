namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Redis Pub/Sub implementation of <see cref="IKyrolusCacheInvalidationBus"/> that broadcasts cache eviction 
/// events across all server instances in a distributed cluster.
/// </summary>
/// <remarks>
/// <b>Real-World Multi-Server Invalidation:</b>
/// When Server 1 updates a record, it publishes a compact message to the Redis invalidation channel. 
/// Servers 2, 3, and 4 receive the event and evict the record from their local L1 memory in real-time.
/// </remarks>
public sealed class KyrolusRedisInvalidationBus : IKyrolusCacheInvalidationBus
{
    private const char MessageSeparator = ':';
    private static readonly char[] KeySeparator = ['\n'];

    private readonly ISubscriber subscriber;
    private readonly KyrolusRedisInvalidationOptions options;
    private readonly RedisChannel channel;

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusRedisInvalidationBus"/>.
    /// </summary>
    /// <param name="multiplexer">The active Redis connection multiplexer.</param>
    /// <param name="options">Optional invalidation options.</param>
    public KyrolusRedisInvalidationBus(IConnectionMultiplexer multiplexer, KyrolusRedisInvalidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        this.options = options ?? new KyrolusRedisInvalidationOptions();
        subscriber = multiplexer.GetSubscriber();
        channel = RedisChannel.Literal(this.options.Channel);
    }

    /// <inheritdoc />
    public Task PublishAsync(KyrolusCacheInvalidationMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Publish)
        {
            return Task.CompletedTask;
        }

        var payload = EncodeMessage(message);
        return subscriber.PublishAsync(channel, payload);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Func<KyrolusCacheInvalidationMessage, Task> handler)
    {
        if (!options.Subscribe)
        {
            return NoopSubscription.Instance;
        }

        var queue = subscriber.Subscribe(channel);
        queue.OnMessage(async message =>
        {
            try
            {
                if (!TryDecodeMessage(message.Message, out var parsed))
                {
                    return;
                }

                await handler(parsed).ConfigureAwait(false);
            }
            catch
            {
                // Invalidation subscriber should not bring down the channel queue on handler failure
            }
        });

        return new RedisSubscription(queue);
    }

    private static string EncodeMessage(KyrolusCacheInvalidationMessage message)
    {
        var payload = string.Join('\n', message.Values);
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        return $"{(int)message.Kind}{MessageSeparator}{data}";
    }

    private static bool TryDecodeMessage(RedisValue message, out KyrolusCacheInvalidationMessage parsed)
    {
        parsed = null!;
        if (message.IsNullOrEmpty)
        {
            return false;
        }

        try
        {
            var text = message.ToString();
            var separatorIndex = text.IndexOf(MessageSeparator);
            if (separatorIndex <= 0 || separatorIndex >= text.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(text[..separatorIndex], out var kindValue))
            {
                return false;
            }

            var payload = text[(separatorIndex + 1)..];
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var values = raw.Split(KeySeparator, StringSplitOptions.RemoveEmptyEntries);
            parsed = new KyrolusCacheInvalidationMessage((KyrolusCacheInvalidationKind)kindValue, values);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class RedisSubscription(ChannelMessageQueue queue) : IDisposable
    {
        public void Dispose() => queue.Unsubscribe();
    }

    private sealed class NoopSubscription : IDisposable
    {
        public static NoopSubscription Instance { get; } = new NoopSubscription();
        public void Dispose() { }
    }
}
