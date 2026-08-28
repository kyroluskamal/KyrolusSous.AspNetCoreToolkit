using System.Collections.Concurrent;
using System.Diagnostics;
using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using KyrolusSous.RabbitMQ.Runtime.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KyrolusSous.RabbitMQ.Runtime.Services;

/// <summary>
/// High-performance RPC client utilizing RabbitMQ Direct-Reply-To feature.
/// </summary>
public sealed class KyrolusRabbitMQRpcClient : IKyrolusRabbitMQRpcClient, IDisposable
{
    private const string ReplyToQueueName = "amq.rabbitmq.reply-to";
    private readonly IKyrolusRabbitMQConnection _connection;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
    private IChannel? _channel;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private bool _consumerStarted;

    public KyrolusRabbitMQRpcClient(IKyrolusRabbitMQConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    private async ValueTask<IChannel> EnsureChannelAndConsumerAsync(CancellationToken cancellationToken = default)
    {
        if (_channel is not null && _channel.IsOpen && _consumerStarted)
        {
            return _channel;
        }

        await _channelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_channel is not null && _channel.IsOpen && _consumerStarted)
            {
                return _channel;
            }

            _channel = await _connection.CreateChannelAsync(cancellationToken).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += (sender, ea) =>
            {
                var correlationId = ea.BasicProperties.CorrelationId;
                if (!string.IsNullOrWhiteSpace(correlationId) && _pendingRequests.TryRemove(correlationId, out var tcs))
                {
                    var responseJson = Encoding.UTF8.GetString(ea.Body.ToArray());
                    tcs.TrySetResult(responseJson);
                }

                return Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(ReplyToQueueName, autoAck: true, consumer: consumer, cancellationToken: cancellationToken).ConfigureAwait(false);
            _consumerStarted = true;

            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task<TResponse> RequestAsync<TRequest, TResponse>(
        string exchange,
        string routingKey,
        TRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var channel = await EnsureChannelAndConsumerAsync(cancellationToken).ConfigureAwait(false);
        var correlationId = Guid.NewGuid().ToString("N");
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[correlationId] = tcs;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        using var ctr = cts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(correlationId, out var removedTcs))
            {
                removedTcs.TrySetException(new TimeoutException($"RPC request timed out after {effectiveTimeout} waiting for correlationId {correlationId}."));
            }
        });

        using var activity = KyrolusRabbitMQInstrumentation.ActivitySource.StartActivity(
            $"RabbitMQ.RPC {exchange}/{routingKey}",
            ActivityKind.Client);

        var headers = new Dictionary<string, object?>();
        KyrolusRabbitMQInstrumentation.InjectTraceContext(headers, activity);

        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = ReplyToQueueName,
            Headers = headers
        };

        var requestJson = JsonSerializer.Serialize(request);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: props,
            body: requestBytes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var responseJson = await tcs.Task.ConfigureAwait(false);
        var response = JsonSerializer.Deserialize<TResponse>(responseJson);

        if (response is null)
        {
            throw new InvalidOperationException("RPC response payload could not be deserialized.");
        }

        return response;
    }

    public void Dispose()
    {
        foreach (var (_, tcs) in _pendingRequests)
        {
            tcs.TrySetCanceled();
        }

        _pendingRequests.Clear();
        _channel?.Dispose();
        _channelLock.Dispose();
    }
}
