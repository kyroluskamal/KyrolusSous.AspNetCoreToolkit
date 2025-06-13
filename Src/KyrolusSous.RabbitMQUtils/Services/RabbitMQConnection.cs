using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

namespace KyrolusSous.RabbitMQUtils.Services;

public class RabbitMQConnection(IConnection connection) : IRabbitMQConnection
{
    private readonly IConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private bool _disposed; // To detect redundant calls

    public IConnection Connection
    {
        get
        {
            return _disposed ? throw new ObjectDisposedException(nameof(RabbitMQConnection)) : _connection;
        }
    }

    // Public implementation of IDisposable
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // Protected implementation of Dispose pattern
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                _connection.Dispose();
            }

            // Dispose unmanaged resources here (if any)

            _disposed = true;
        }
    }

    // Finalizer
    ~RabbitMQConnection()
    {
        Dispose(false);
    }
}
