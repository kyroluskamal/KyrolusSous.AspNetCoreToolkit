using System.Diagnostics;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusNoopValidationTracer : IKyrolusValidationTracer
{
    public static readonly IKyrolusValidationTracer Instance = new KyrolusNoopValidationTracer();

    public object? Start(KyrolusValidationTraceContext context) => null;

    public ValueTask StopAsync(
        KyrolusValidationTraceContext context,
        object? state,
        IReadOnlyList<KyrolusValidationFailure> failures,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class KyrolusValidationActivityTracer(string sourceName = "Kyrolus.Validation")
    : IKyrolusValidationTracer
{
    private readonly ActivitySource source = new(sourceName);

    public object? Start(KyrolusValidationTraceContext context)
    {
        var activity = source.StartActivity("Validation", ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("validation.request_type", context.RequestType?.FullName);
        activity.SetTag("validation.rule_sets", context.Context.RuleSets is { Count: > 0 }
            ? string.Join(",", context.Context.RuleSets)
            : null);
        activity.SetTag("validation.groups", context.Context.Groups is { Count: > 0 }
            ? string.Join(",", context.Context.Groups)
            : null);
        activity.SetTag("validation.min_severity", context.Context.MinimumSeverity?.ToString());

        return activity;
    }

    public ValueTask StopAsync(
        KyrolusValidationTraceContext context,
        object? state,
        IReadOnlyList<KyrolusValidationFailure> failures,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        if (state is Activity activity)
        {
            activity.SetTag("validation.failures", failures.Count);
            if (failures.Count > 0)
            {
                var maxSeverity = failures.Max(f => f.Severity);
                activity.SetTag("validation.max_severity", maxSeverity.ToString());
            }

            if (exception is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.SetTag("validation.exception", exception.GetType().FullName);
            }

            activity.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
