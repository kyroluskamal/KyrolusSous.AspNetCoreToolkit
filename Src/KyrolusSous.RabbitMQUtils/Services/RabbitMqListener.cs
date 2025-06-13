
using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

namespace KyrolusSous.RabbitMQUtils.Services;

public class RabbitMqListener(IRabbitMQConnection rabbitMqConnection) : IRabbitMqListener
{
    readonly IChannel channel = rabbitMqConnection.Connection.CreateChannelAsync().GetAwaiter().GetResult();


    public async Task ConsumeAsync<TEvent>(string queue, Func<TEvent, Task> action,
                                         bool durable = true, bool exclusive = false,
                                         bool autoDelete = false, bool autoAck = false,
                                         IDictionary<string, object?>? arguments = null)
    {
        await channel.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var eventToPublish = JsonSerializer.Deserialize<TEvent>(message);

                if (!Equals(eventToPublish, default(TEvent)))
                {
                    Console.WriteLine($"📥 [RabbitMQ] Processing message: {message}");

                    await action(eventToPublish!);

                    if (!autoAck)
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                        Console.WriteLine($"✅ [RabbitMQ] Message Acknowledged: {message}");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ [RabbitMQ] Received NULL message, rejecting...");
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [RabbitMQ] Error processing message: {ex.Message}");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        // ✅ تمرير autoAck بالشكل الصحيح
        await channel.BasicConsumeAsync(queue: queue,
                                        autoAck: autoAck,  // ✅ الآن يتم تمريره ديناميكيًا
                                        consumer: consumer);

        Console.WriteLine($"✅ [RabbitMQ] Listening on queue: {queue} (autoAck={autoAck})");
    }

}
