using System.Text;
using System.Text.Json;
using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQUtils.Services;

public class RabbitMQUtils(IRabbitMQConnection rabbitMqConnection) : IRabbitMQUtils
{
    private IChannel? _channel;

    private IChannel Channel => _channel ??= rabbitMqConnection.Connection.CreateChannelAsync().GetAwaiter().GetResult();

    public async Task PublishAsync<TEvent>(string exchange, string routingKey, TEvent body, bool mandatory = true, BasicProperties? basicProperties = null)
    {
        var message = JsonSerializer.Serialize(body);
        var _body = Encoding.UTF8.GetBytes(message);
        var basicProps = basicProperties ?? new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent
        };
        Console.WriteLine($"📤 Sending message to Exchange: {exchange}, Routing Key: {routingKey}");
        Console.WriteLine($"➡️ Message: {message}");
        if (Channel.IsClosed)
        {
            Console.WriteLine("🚨 ERROR: Channel is closed, recreating...");
            _channel = rabbitMqConnection.Connection.CreateChannelAsync().GetAwaiter().GetResult();
        }
        await Channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: basicProps,
            body: _body
        );
        Console.WriteLine("✅ Message published successfully!");
    }

    public async Task SetupQueueAsync(string exchange, IQueueSetup[] queues, string type = ExchangeType.Direct, bool isDurable = true, bool autoDelete = false, IDictionary<string, object?>? arguments = null)
    {
        // var args = new Dictionary<string, object>
        //     {
        //         { "x-dead-letter-exchange", "dlx.exchange" }, // ✅ اسم الـ DLX Exchange
        //         { "x-dead-letter-routing-key", "dlx.routingKey" } // ✅ Routing Key للـ DLX
        //     };
        // we should create new exchange and new queues for failed message and create consumers for these new queues to try to process the failed message
        await Channel.ExchangeDeclareAsync(exchange, type, isDurable, false, arguments);
        foreach (var q in queues)
        {
            await Channel.QueueDeclareAsync(q.Name, q.Durable, q.Exclusive, q.Autodelete, q.Arguments);
            await Channel.QueueBindAsync(q.Name, exchange, q.RoutingKey);
        }
    }
}
