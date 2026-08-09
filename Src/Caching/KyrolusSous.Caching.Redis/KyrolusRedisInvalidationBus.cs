using System.Text;
using KyrolusSous.Caching.Abstractions;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisInvalidationBus : IKyrolusCacheInvalidationBus
{
    private const char MessageSeparator = ':';
    private static readonly char[] KeySeparator = ['\n'];

    private readonly ISubscriber subscriber;
    private readonly KyrolusRedisInvalidationOptions options;
    private readonly RedisChannel channel;

    public KyrolusRedisInvalidationBus(IConnectionMultiplexer multiplexer, KyrolusRedisInvalidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        this.options = options ?? new KyrolusRedisInvalidationOptions();
        subscriber = multiplexer.GetSubscriber();
        channel = RedisChannel.Literal(this.options.Channel);
    }

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

    public IDisposable Subscribe(Func<KyrolusCacheInvalidationMessage, Task> handler)
    {
        if (!options.Subscribe)
        {
            return NoopSubscription.Instance;
        }

        var queue = subscriber.Subscribe(channel);
        queue.OnMessage(message =>
        {
            if (!TryDecodeMessage(message.Message, out var parsed))
            {
                return;
            }

            _ = handler(parsed);
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
