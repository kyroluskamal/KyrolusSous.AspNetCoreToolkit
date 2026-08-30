namespace KyrolusSous.ExceptionHandling.Runtime.Helpers;

public class KyrolusErrorContextInfo
{
    public string RequestPath { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? EndpointName { get; set; }

    public KyrolusErrorContextInfo() { }

    public KyrolusErrorContextInfo(HttpContext? context)
    {
        if (context is null) return;

        RequestPath = context.Request.Path.ToString();
        HttpMethod = context.Request.Method;

        var routeValues = context.Request.RouteValues;
        Controller = routeValues["controller"]?.ToString();
        Action = routeValues["action"]?.ToString();

        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
            EndpointName = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                            ?? endpoint.DisplayName;
    }
}
