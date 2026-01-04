using System.Globalization;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace KyrolusSous.DataProtection.Redis;

public sealed class KyrolusRedisKeyRingRefreshNotifier(
    IConnectionMultiplexer connection,
    IOptions<KyrolusRedisKeyRingRefreshOptions> redisOptions,
    IOptions<KyrolusDataProtectionOptions> dataProtectionOptions,
    ILogger<KyrolusRedisKeyRingRefreshNotifier> logger)
    : IKyrolusKeyRingRefreshNotifier
{
    private readonly IConnectionMultiplexer connection =
        connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly KyrolusRedisKeyRingRefreshOptions redisOptions =
        redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
    private readonly KyrolusDataProtectionOptions dataProtectionOptions =
        dataProtectionOptions?.Value ?? throw new ArgumentNullException(nameof(dataProtectionOptions));
    private readonly ILogger<KyrolusRedisKeyRingRefreshNotifier> logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task PublishAsync(
        KyrolusKeyRingRefreshSignal signal,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = Serialize(signal);
        var channel = GetChannel();
        await connection.GetSubscriber().PublishAsync(channel, payload).ConfigureAwait(false);
    }

    public async Task ListenAsync(
        Func<KyrolusKeyRingRefreshSignal, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        var channel = GetChannel();
        var subscriber = connection.GetSubscriber();

        await subscriber.SubscribeAsync(channel, (channelName, value) =>
        {
            _ = HandleMessageAsync(value, handler, cancellationToken);
        }).ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await subscriber.UnsubscribeAsync(channel).ConfigureAwait(false);
        }
    }

    private async Task HandleMessageAsync(
        RedisValue value,
        Func<KyrolusKeyRingRefreshSignal, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryDeserialize(value, out var signal))
            {
                return;
            }

            await handler(signal, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to handle key ring refresh signal.");
        }
    }

    private RedisChannel GetChannel()
    {
        var channel = redisOptions.Channel;
        if (redisOptions.IncludeApplicationNameInChannel &&
            !string.IsNullOrWhiteSpace(dataProtectionOptions.ApplicationName))
        {
            channel = $"{channel}:{dataProtectionOptions.ApplicationName}";
        }

        return new RedisChannel(channel, RedisChannel.PatternMode.Literal);
    }

    private static string Serialize(KyrolusKeyRingRefreshSignal signal)
    {
        return string.Join(
            '|',
            Escape(signal.ApplicationName),
            Escape(signal.InstanceId),
            signal.OccurredAt.UtcTicks.ToString(CultureInfo.InvariantCulture),
            ((int)signal.Reason).ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryDeserialize(
        RedisValue value,
        out KyrolusKeyRingRefreshSignal signal)
    {
        signal = default!;

        if (!value.HasValue)
        {
            return false;
        }

        var parts = value.ToString().Split('|');
        if (parts.Length < 4)
        {
            return false;
        }

        if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
        {
            return false;
        }

        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var reasonValue))
        {
            return false;
        }

        var reason = Enum.IsDefined(typeof(KyrolusKeyRingRefreshReason), reasonValue)
            ? (KyrolusKeyRingRefreshReason)reasonValue
            : KyrolusKeyRingRefreshReason.Unknown;

        signal = new KyrolusKeyRingRefreshSignal(
            Unescape(parts[0]),
            Unescape(parts[1]),
            new DateTimeOffset(ticks, TimeSpan.Zero),
            reason);
        return true;
    }

    private static string Escape(string value)
        => Uri.EscapeDataString(value ?? string.Empty);

    private static string Unescape(string value)
        => Uri.UnescapeDataString(value ?? string.Empty);
}
