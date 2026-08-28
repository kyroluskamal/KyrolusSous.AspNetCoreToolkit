using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Runtime.Services
{
    /// <summary>
    /// Thread-safe managed RabbitMQ connection implementation.
    /// </summary>
    public class KyrolusRabbitMQConnection : IKyrolusRabbitMQConnection, global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQConnection
    {
        private readonly IConnectionFactory _connectionFactory;
        private IConnection? _connection;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _disposed;

        public KyrolusRabbitMQConnection(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IConnection Connection
        {
            get
            {
                if (_connection is not null && _connection.IsOpen)
                {
                    return _connection;
                }

                _connectionLock.Wait();
                try
                {
                    if (_connection is not null && _connection.IsOpen)
                    {
                        return _connection;
                    }

                    _connection = _connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
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
                return await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_connection is null || !_connection.IsOpen)
                {
                    _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
                }

                return await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _connectionLock.Release();
            }
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
