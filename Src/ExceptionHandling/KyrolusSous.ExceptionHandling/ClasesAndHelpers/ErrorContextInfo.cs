using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace KyrolusSous.ExceptionHandling.ClasesAndHelpers;

public class ErrorContextInfo
{
    public string RequestPath { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? EndpointName { get; set; }

    public ErrorContextInfo() { }

    public ErrorContextInfo(HttpContext? context)
    {
        if (context is null) return;

        RequestPath = context.Request?.Path.Value ?? string.Empty;
        HttpMethod = context.Request?.Method ?? string.Empty;

        var routeValues = context.GetRouteData()?.Values;
        Controller = routeValues?["controller"]?.ToString();
        Action = routeValues?["action"]?.ToString();

        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            EndpointName = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                           ?? endpoint.DisplayName;
        }
    }
}
