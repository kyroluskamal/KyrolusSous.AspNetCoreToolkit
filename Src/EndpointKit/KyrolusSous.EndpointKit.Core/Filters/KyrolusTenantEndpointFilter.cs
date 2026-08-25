using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.EndpointKit.Core.Filters;

/// <summary>
/// Minimal API Endpoint Filter that automatically resolves the tenant ID from
/// HTTP Header (X-Tenant-ID), JWT Claims (tenant_id / tenant), or Query String (tenantId).
/// </summary>
public sealed class KyrolusTenantEndpointFilter(
    string headerName = "X-Tenant-ID",
    string claimType = "tenant_id",
    string queryParamName = "tenantId",
    bool requireTenant = false) : IEndpointFilter
{
    public const string TenantItemKey = "KyrolusTenantId";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        string? tenantId = null;

        // 1. Check Header
        if (httpContext.Request.Headers.TryGetValue(headerName, out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
        {
            tenantId = headerVal.ToString().Trim();
        }

        // 2. Check JWT Claims
        if (string.IsNullOrWhiteSpace(tenantId) && httpContext.User.Identity?.IsAuthenticated == true)
        {
            tenantId = (httpContext.User.FindFirst(claimType)?.Value
                       ?? httpContext.User.FindFirst("tenant")?.Value
                       ?? httpContext.User.FindFirst(ClaimTypes.GroupSid)?.Value)?.Trim();
        }

        // 3. Check Query Parameter
        if (string.IsNullOrWhiteSpace(tenantId) && httpContext.Request.Query.TryGetValue(queryParamName, out var queryVal) && !string.IsNullOrWhiteSpace(queryVal))
        {
            tenantId = queryVal.ToString().Trim();
        }

        if (requireTenant && string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.Problem(
                title: "Tenant Required",
                detail: $"A valid tenant identifier is required via header '{headerName}', query '{queryParamName}', or user claims.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            httpContext.Items[TenantItemKey] = tenantId.Trim();
        }

        return await next(context);
    }
}
