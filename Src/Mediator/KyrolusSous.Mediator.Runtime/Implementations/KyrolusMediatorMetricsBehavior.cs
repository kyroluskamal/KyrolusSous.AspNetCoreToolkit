using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Holds the shared <see cref="Meter"/> and instruments for <see cref="KyrolusMediatorMetricsBehavior{TRequest, TResponse}"/>.
/// </summary>
/// <remarks>
/// Kept on a non-generic type deliberately: a <see langword="static"/> field on a generic class gets
/// its own storage per closed instantiation, so putting the <see cref="Meter"/> directly on
/// <see cref="KyrolusMediatorMetricsBehavior{TRequest, TResponse}"/> would create one <see cref="Meter"/>
/// (and one set of instruments) per distinct (request, response) pair instead of one for the whole
/// pipeline.
/// </remarks>
internal static class KyrolusMediatorMetrics
{
    internal static readonly Meter Meter = new("Kyrolus.Mediator");

    internal static readonly Counter<long> RequestCount = Meter.CreateCounter<long>(
        "kyrolus.mediator.requests",
        unit: "{request}",
        description: "Number of requests dispatched through the mediator pipeline, tagged by request type and outcome.");

    internal static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "kyrolus.mediator.duration",
        unit: "ms",
        description: "Time taken for a request to complete through the mediator pipeline, from this behavior inward.");
}

/// <summary>
/// Optional pipeline behavior that records request-dispatch metrics via <see cref="System.Diagnostics.Metrics"/> -
/// the standard .NET metrics API - so any OpenTelemetry-configured app (or any other
/// <c>System.Diagnostics.Metrics</c> listener) picks up its instruments automatically by listening
/// for the <c>"Kyrolus.Mediator"</c> meter name, without this package needing a direct reference to
/// any OpenTelemetry package - mirroring how <c>KyrolusValidationSystemMetrics</c> does the same for
/// the Validation pipeline. Not registered by default.
/// </summary>
/// <remarks>
/// Publishes two instruments, both tagged with <c>mediator.request_type</c> (the request type's
/// simple name) and <c>mediator.outcome</c> (<c>"succeeded"</c> or <c>"faulted"</c>):
/// <list type="bullet">
/// <item><description><c>kyrolus.mediator.requests</c> (<see cref="Counter{T}">Counter&lt;long&gt;</see>) - incremented once per dispatched request.</description></item>
/// <item><description><c>kyrolus.mediator.duration</c> (<see cref="Histogram{T}">Histogram&lt;double&gt;</see>, milliseconds) - the request's end-to-end time through the rest of the pipeline.</description></item>
/// </list>
/// Registered like any other open-generic behavior; when published ahead of time,
/// <c>KyrolusSous.Mediator.Generator</c> closes it at compile time for every (request, response) pair
/// it can see, the same way it closes any other <c>AddOpenBehavior</c> registration.
/// </remarks>
/// <example>
/// <code>
/// services.AddKyrolusMediator(configuration =&gt;
/// {
///     configuration.RegisterServicesFromAssemblyContaining&lt;Program&gt;();
///     configuration.AddKyrolusMediatorMetrics();
/// });
///
/// // Wherever OpenTelemetry is configured:
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(metrics =&gt; metrics.AddMeter("Kyrolus.Mediator"));
/// </code>
/// </example>
public sealed class KyrolusMediatorMetricsBehavior<TRequest, TResponse> : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).Name;
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            Record(requestType, outcome: "succeeded", startTimestamp);
            return response;
        }
        catch
        {
            Record(requestType, outcome: "faulted", startTimestamp);
            throw;
        }
    }

    private static void Record(string requestType, string outcome, long startTimestamp)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("mediator.request_type", requestType),
            new("mediator.outcome", outcome)
        };

        KyrolusMediatorMetrics.RequestCount.Add(1, tags);
        KyrolusMediatorMetrics.Duration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, tags);
    }
}
