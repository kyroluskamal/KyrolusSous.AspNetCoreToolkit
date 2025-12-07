using System;

namespace KyrolusSous.Logging.Tests;

public static class MyCorrelationIdContext
{
    private static readonly AsyncLocal<string> _correlationId = new();

    public static void SetCorrelationId(string correlationId) => _correlationId.Value = correlationId;

    public static string? GetCorrelationId() => _correlationId.Value;
}

public class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = MyCorrelationIdContext.GetCorrelationId();

        if (!string.IsNullOrEmpty(correlationId))
        {
            var property = propertyFactory.CreateProperty("CorrelationId", correlationId);
            logEvent.AddPropertyIfAbsent(property);
        }
    }
}