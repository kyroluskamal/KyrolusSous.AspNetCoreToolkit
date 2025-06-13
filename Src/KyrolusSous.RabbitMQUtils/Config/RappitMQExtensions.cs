using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;
using KyrolusSous.RabbitMQUtils.Services;

namespace KyrolusSous.RabbitMQUtils.Config;

public static class RappitMQExtensions
{
    public static void AddRabbitMQ(this IServiceCollection services, string hostName = "rabbitmqbus", string userName = "CodingBible", string password = "coding123", int? sslPort = 5671, int httpPort = 5672)
    {
        services.AddSingleton<IConnectionFactory>(sp => new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = sslPort == null ? httpPort : (int)sslPort,
            Ssl = new SslOption
            {
                Enabled = true,
                ServerName = hostName,
                AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
                                 SslPolicyErrors.RemoteCertificateNameMismatch
            }
        });
        services.AddSingleton<IRabbitMQConnection, RabbitMQConnection>(sp =>
        {
            var factory = sp.GetRequiredService<IConnectionFactory>();
            var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            return new RabbitMQConnection(connection);
        });
        services.AddScoped<IRabbitMQUtils, Services.RabbitMQUtils>();

        services.AddScoped<IRabbitMqListener, RabbitMqListener>();

    }
}
