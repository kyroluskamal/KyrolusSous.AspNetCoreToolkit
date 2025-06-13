namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

public interface IRabbitMQConnection : IDisposable
{
    IConnection Connection { get; }
}
