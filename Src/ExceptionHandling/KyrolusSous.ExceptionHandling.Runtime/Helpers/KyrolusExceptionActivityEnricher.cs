namespace KyrolusSous.ExceptionHandling.Runtime.Helpers;

public static class KyrolusExceptionActivityEnricher
{
    public static void Enrich(Activity? activity, KyrolusExceptionMapping mapping, KyrolusErrorContext context, Exception exception, bool includeDetails)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, mapping.Error.Code);
        activity.SetTag("kyrolus.error_code", mapping.Error.Code);
        activity.SetTag("http.status_code", (int)mapping.StatusCode);

        if (!string.IsNullOrWhiteSpace(context.TraceId))
            activity.SetTag("kyrolus.trace_id", context.TraceId);

        if (!string.IsNullOrWhiteSpace(context.CorrelationId))
            activity.SetTag("kyrolus.correlation_id", context.CorrelationId);

        if (!string.IsNullOrWhiteSpace(context.UserId))
            activity.SetTag("enduser.id", context.UserId);

        if (!string.IsNullOrWhiteSpace(context.TenantId))
            activity.SetTag("kyrolus.tenant_id", context.TenantId);

        if (!string.IsNullOrWhiteSpace(context.Path))
            activity.SetTag("http.target", context.Path);

        if (!string.IsNullOrWhiteSpace(context.Method))
            activity.SetTag("http.method", context.Method);

        activity.SetTag("exception.type", exception.GetType().FullName);

        if (includeDetails)
        {
            activity.SetTag("exception.message", exception.Message);
            activity.SetTag("exception.stacktrace", exception.StackTrace);
        }
    }
}
