using System.Diagnostics;

namespace KyrolusSous.RabbitMQ.Runtime.Diagnostics;

/// <summary>
/// OpenTelemetry and ActivitySource instrumentation for Kyrolus RabbitMQ messaging.
/// </summary>
public static class KyrolusRabbitMQInstrumentation
{
    public const string ActivitySourceName = "KyrolusSous.RabbitMQ";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public static void InjectTraceContext(IDictionary<string, object?> headers, Activity? activity)
    {
        if (activity is null) return;

        headers[TraceParentHeader] = activity.Id;
        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            headers[TraceStateHeader] = activity.TraceStateString;
        }
    }

    public static ActivityContext ExtractTraceContext(IDictionary<string, object?>? headers)
    {
        if (headers is null) return default;

        string? traceparent = null;
        string? tracestate = null;

        if (headers.TryGetValue(TraceParentHeader, out var tpObj))
        {
            traceparent = tpObj switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string str => str,
                _ => tpObj?.ToString()
            };
        }

        if (headers.TryGetValue(TraceStateHeader, out var tsObj))
        {
            tracestate = tsObj switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string str => str,
                _ => tsObj?.ToString()
            };
        }

        if (!string.IsNullOrWhiteSpace(traceparent) && ActivityContext.TryParse(traceparent, tracestate, out var context))
        {
            return context;
        }

        return default;
    }
}
