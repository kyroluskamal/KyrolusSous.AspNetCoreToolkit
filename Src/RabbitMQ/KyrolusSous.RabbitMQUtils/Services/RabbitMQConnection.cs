using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQUtils.Services;

public class RabbitMQConnection : IKyrolusRabbitMQConnection, IRabbitMQConnection
{
    private readonly IConnectionFactory? _factory;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public RabbitMQConnection(IConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public RabbitMQConnection(IConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IConnection Connection
    {
        get
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMQConnection));
            if (_connection is not null) return _connection;

            _lock.Wait();
            try
            {
                return _connection ??= _factory!.CreateConnectionAsync().GetAwaiter().GetResult();
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RabbitMQConnection));
        if (_connection is null && _factory is not null)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _connection ??= await _factory.CreateConnectionAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        return await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _connection?.Dispose();
                _lock.Dispose();
            }

            _disposed = true;
        }
    }

    ~RabbitMQConnection()
    {
        Dispose(false);
    }
}
