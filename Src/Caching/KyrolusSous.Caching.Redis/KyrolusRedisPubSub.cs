namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Type-safe Redis Pub/Sub messaging implementation of <see cref="IKyrolusRedisPubSub"/>.
/// </summary>
/// <remarks>
/// <b>Real-World Use Cases:</b>
/// <list type="bullet">
///   <item><description><b>Real-time User Notifications &amp; WebSockets:</b> When a user's order status changes, broadcasting an <c>OrderStatusUpdatedEvent</c> to all web nodes to push instant notifications through SignalR.</description></item>
///   <item><description><b>Microservice Cache Sync:</b> Notifying worker services when reference tables (e.g. Tax rates, shipping zones) change.</description></item>
/// </list>
/// </remarks>
public sealed class KyrolusRedisPubSub : IKyrolusRedisPubSub
{
    private readonly IConnectionMultiplexer multiplexer;
    private readonly ISubscriber subscriber;
    private readonly IKyrolusCacheSerializer serializer;
    private readonly IKyrolusCacheKeyFactory keyFactory;
    private readonly KyrolusRedisCacheOptions options;

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusRedisPubSub"/>.
    /// </summary>
    /// <param name="multiplexer">The active Redis connection multiplexer.</param>
    /// <param name="serializer">The serialization engine.</param>
    /// <param name="keyFactory">Optional key factory.</param>
    /// <param name="options">Optional Redis options.</param>
    public KyrolusRedisPubSub(
        IConnectionMultiplexer multiplexer,
        IKyrolusCacheSerializer serializer,
        IKyrolusCacheKeyFactory? keyFactory = null,
        KyrolusRedisCacheOptions? options = null)
    {
        this.multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
        this.subscriber = multiplexer.GetSubscriber();
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.options = options ?? new KyrolusRedisCacheOptions();
        this.keyFactory = keyFactory ?? new KyrolusCacheKeyFactory(this.options.KeyPrefix);
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(string channel, T message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedChannel = keyFactory.BuildKey(channel).ToString();
        var payload = serializer.Serialize(message);

        await subscriber.PublishAsync(
            RedisChannel.Literal(resolvedChannel),
            payload,
            options.WriteCommandFlags).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> SubscribeAsync<T>(string channel, Func<T, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedChannel = keyFactory.BuildKey(channel).ToString();
        var redisChannel = RedisChannel.Literal(resolvedChannel);

        await subscriber.SubscribeAsync(redisChannel, async (_, value) =>
        {
            if (value.IsNullOrEmpty) return;
            try
            {
                var deserialized = serializer.Deserialize<T>(value!);
                if (deserialized is not null)
                {
                    await handler(deserialized).ConfigureAwait(false);
                }
            }
            catch
            {
                // Best-effort message handling
            }
        }, options.ReadCommandFlags).ConfigureAwait(false);

        return new SubscriptionHandle(subscriber, redisChannel, options.WriteCommandFlags);
    }

    private sealed class SubscriptionHandle : IAsyncDisposable
    {
        private readonly ISubscriber subscriber;
        private readonly RedisChannel channel;
        private readonly CommandFlags flags;
        private int unsubscribed;

        public SubscriptionHandle(ISubscriber subscriber, RedisChannel channel, CommandFlags flags)
        {
            this.subscriber = subscriber;
            this.channel = channel;
            this.flags = flags;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref unsubscribed, 1) == 0)
            {
                await subscriber.UnsubscribeAsync(channel, flags: flags).ConfigureAwait(false);
            }
        }
    }
}
