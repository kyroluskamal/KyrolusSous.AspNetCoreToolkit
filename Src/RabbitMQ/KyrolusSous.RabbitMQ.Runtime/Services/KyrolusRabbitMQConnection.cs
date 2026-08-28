using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Runtime.Services
{
    /// <summary>
    /// Thread-safe managed RabbitMQ connection implementation with automatic recovery, shutdown telemetry, and disposed state protection.
    /// </summary>
    public class KyrolusRabbitMQConnection : IKyrolusRabbitMQConnection, global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQConnection
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger<KyrolusRabbitMQConnection> _logger;
        private IConnection? _connection;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _disposed;

        public KyrolusRabbitMQConnection(
            IConnectionFactory connectionFactory,
            ILogger<KyrolusRabbitMQConnection>? logger = null)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? NullLogger<KyrolusRabbitMQConnection>.Instance;
        }

        public IConnection Connection
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_connection is not null && _connection.IsOpen)
                {
                    return _connection;
                }

                _connectionLock.Wait();
                try
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);

                    if (_connection is not null && _connection.IsOpen)
                    {
                        return _connection;
                    }

                    _connection = _connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
                    RegisterConnectionEvents(_connection);
                    return _connection;
                }
                finally
                {
                    _connectionLock.Release();
                }
            }
        }

        public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_connection is not null && _connection.IsOpen)
            {
                try
                {
                    return await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // If connection just closed, fall through to reconnect under lock
                }
            }

            await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_connection is null || !_connection.IsOpen)
                {
                    _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
                    RegisterConnectionEvents(_connection);
                }

                return await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void RegisterConnectionEvents(IConnection connection)
        {
            connection.ConnectionShutdownAsync += (sender, ea) =>
            {
                _logger.LogWarning("RabbitMQ connection shutdown initiated by {Initiator}: {ReplyText} (code {ReplyCode})",
                    ea.Initiator, ea.ReplyText, ea.ReplyCode);
                return Task.CompletedTask;
            };

            connection.RecoverySucceededAsync += (sender, ea) =>
            {
                _logger.LogInformation("RabbitMQ connection recovery succeeded.");
                return Task.CompletedTask;
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _connection?.Dispose();
            }
            catch { }

            _connectionLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

namespace KyrolusSous.RabbitMQUtils.Services
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Runtime.Services.KyrolusRabbitMQConnection"/>.
    /// </summary>
    public class RabbitMQConnection : global::KyrolusSous.RabbitMQ.Runtime.Services.KyrolusRabbitMQConnection
    {
        public RabbitMQConnection(IConnectionFactory connectionFactory) : base(connectionFactory)
        {
        }
    }
}
