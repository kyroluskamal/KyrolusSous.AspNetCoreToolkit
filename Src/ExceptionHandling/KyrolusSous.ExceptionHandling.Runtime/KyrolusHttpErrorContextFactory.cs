using Microsoft.AspNetCore.Localization;

namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusHttpErrorContextFactory(
    IOptions<KyrolusExceptionHandlingOptions> options,
    IHttpContextAccessor? accessor = null)
{
    private readonly IHttpContextAccessor? accessor = accessor;
    private readonly KyrolusExceptionHandlingOptions options = options.Value;

    public KyrolusErrorContext Create(HttpContext? context = null)
    {
        context ??= accessor?.HttpContext;
        if (context is null)
            return new KyrolusErrorContext(
                TraceId: Activity.Current?.Id,
                CorrelationId: null,
                UserId: null,
                TenantId: null,
                Path: null,
                Method: null,
                Culture: null);

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
        if (context.Request.Headers.TryGetValue(headerName, out var header) && !string.IsNullOrWhiteSpace(header))
            return header.ToString();

        return context.TraceIdentifier;
    }

    private static readonly HashSet<string> PredefinedCultureNames = new(
        CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Select(c => c.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name)),
        StringComparer.OrdinalIgnoreCase);

    private static CultureInfo? ResolveCulture(HttpContext context)
    {
        var cultureFeature = context.Features.Get<IRequestCultureFeature>();
        if (cultureFeature?.RequestCulture.Culture is not null)
            return cultureFeature.RequestCulture.Culture;

        if (context.Request.Headers.TryGetValue("Accept-Language", out var languages))
        {
            var rawLanguages = languages.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var lang in rawLanguages)
            {
                var cultureName = lang.Split(';')[0].Trim();
                if (string.IsNullOrWhiteSpace(cultureName) || cultureName == "*") continue;

                if (!PredefinedCultureNames.Contains(cultureName))
                {
                    continue;
                }

                try
                {
                    return CultureInfo.GetCultureInfo(cultureName);
                }
                catch (CultureNotFoundException)
                {
                    continue;
                }
            }
        }

        return null;
    }

    private static string? ResolveClaim(ClaimsPrincipal user, string? claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType)) return null;
        return user.FindFirstValue(claimType);
    }
}
