namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

public interface IRabbitMQUtils
{
    Task SetupQueueAsync(string exchange, IQueueSetup[] queues, string type = ExchangeType.Direct, bool isDurable = true, bool autoDelete = false, IDictionary<string, object?>? arguments = null);
    Task PublishAsync<TEvent>(string exchange, string routingKey, TEvent body, bool mandatory = true, BasicProperties? basicProperties = null);
}
