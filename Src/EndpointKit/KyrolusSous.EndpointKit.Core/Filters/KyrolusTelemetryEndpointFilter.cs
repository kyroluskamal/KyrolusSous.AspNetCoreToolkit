using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.EndpointKit.Core.Filters;

/// <summary>
/// Minimal API Endpoint Filter that enriches OpenTelemetry Activity with EndpointKit metadata.
/// </summary>
public sealed class KyrolusTelemetryEndpointFilter(string entityName, string actionName) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("endpointkit.entity", entityName);
            activity.SetTag("endpointkit.action", actionName);
            activity.SetTag("endpointkit.route", context.HttpContext.Request.Path.Value);
        }

        try
        {
            var result = await next(context);
            if (activity is not null && context.HttpContext.Response.StatusCode >= 400)
            {
                activity.SetStatus(ActivityStatusCode.Error, $"HTTP {context.HttpContext.Response.StatusCode}");
            }
            return result;
        }
        catch (Exception ex)
        {
            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.SetTag("exception.message", ex.Message);
            }
            throw;
        }
    }
}
