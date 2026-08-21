namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusHttpErrorContextFactory(IHttpContextAccessor accessor, IOptions<KyrolusExceptionHandlingOptions> options)
{
    private readonly IHttpContextAccessor accessor = accessor;
    private readonly KyrolusExceptionHandlingOptions options = options.Value;

    public KyrolusErrorContext Create(Exception exception)
    {
        var context = accessor.HttpContext;
        if (context is null)
        {
            return new KyrolusErrorContext(
                TraceId: Activity.Current?.Id,
                CorrelationId: null,
                UserId: null,
                TenantId: null,
                Path: null,
                Method: null,
                Culture: null);
        }

        var traceId = options.IncludeTraceId ? (Activity.Current?.Id ?? context.TraceIdentifier) : null;
        var correlationId = options.IncludeCorrelationId
            ? ResolveCorrelationId(context, options.CorrelationIdHeaderName)
            : null;

        var culture = ResolveCulture(context);
        var userId = ResolveClaim(context.User, options.UserIdClaimType);
        var tenantId = ResolveClaim(context.User, options.TenantIdClaimType);

        return new KyrolusErrorContext(
            traceId,
            correlationId,
            userId,
            tenantId,
            context.Request.Path.Value,
            context.Request.Method,
            culture);
    }

    private static string? ResolveCorrelationId(HttpContext context, string headerName)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var header))
        {
            return header.ToString();
        }

        return context.TraceIdentifier;
    }

    private static CultureInfo? ResolveCulture(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Accept-Language", out var languages))
        {
            var cultureName = languages.ToString().Split(',').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(cultureName))
            {
                try
                {
                    return CultureInfo.GetCultureInfo(cultureName);
                }
                catch (CultureNotFoundException)
                {
                    return null;
                }
            }
        }

        return null;
    }

    private static string? ResolveClaim(ClaimsPrincipal user, string? claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType))
        {
            return null;
        }

        return user.FindFirstValue(claimType);
    }
}
