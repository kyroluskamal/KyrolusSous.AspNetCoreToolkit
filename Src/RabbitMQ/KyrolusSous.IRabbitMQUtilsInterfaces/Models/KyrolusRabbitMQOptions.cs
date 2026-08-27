namespace KyrolusSous.IRabbitMQUtilsInterfaces.Models;

/// <summary>
/// Comprehensive configuration options for Kyrolus RabbitMQ.
/// </summary>
public sealed class KyrolusRabbitMQOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public bool SslEnabled { get; set; }
    public string? SslServerName { get; set; }
    public ushort PrefetchCount { get; set; } = 50;
    public TimeSpan RequestedHeartbeat { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);
    public bool AutomaticRecoveryEnabled { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryInitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public double RetryBackoffMultiplier { get; set; } = 2.0;
    public bool UseDeadLetterExchange { get; set; } = true;
    public string DlxExchangeName { get; set; } = "dlx.exchange";
    public string DlxRoutingKeyPrefix { get; set; } = "dlx.";
}
