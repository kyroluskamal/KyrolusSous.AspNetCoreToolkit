namespace KyrolusSous.RabbitMQ.Abstractions.Models
{
    /// <summary>
    /// Structured message envelope for distributed message propagation, correlation, and tracing.
    /// </summary>
    public sealed class KyrolusMessageEnvelope<T>
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
        public string? CorrelationId { get; set; }
        public string? CausationId { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public string MessageType { get; set; } = typeof(T).FullName ?? typeof(T).Name;
        public T? Payload { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];

        public KyrolusMessageEnvelope() { }

        public KyrolusMessageEnvelope(T payload, string? correlationId = null, string? causationId = null)
        {
            Payload = payload;
            CorrelationId = correlationId;
            CausationId = causationId;
        }
    }
}

namespace KyrolusSous.IRabbitMQUtilsInterfaces.Models
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Models.KyrolusMessageEnvelope{T}"/>.
    /// </summary>
    public sealed class KyrolusMessageEnvelope<T>
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
        public string? CorrelationId { get; set; }
        public string? CausationId { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public string MessageType { get; set; } = typeof(T).FullName ?? typeof(T).Name;
        public T? Payload { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];

        public KyrolusMessageEnvelope() { }

        public KyrolusMessageEnvelope(T payload, string? correlationId = null, string? causationId = null)
        {
            Payload = payload;
            CorrelationId = correlationId;
            CausationId = causationId;
        }
    }
}
