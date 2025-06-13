namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

public interface IRabbitMqListener
{
    Task ConsumeAsync<TEvent>(string queue, Func<TEvent, Task> action, bool durable = true, bool exclusive = false, bool autoDelete = false, bool autoAck = true, IDictionary<string, object?>? arguments = null);
}
