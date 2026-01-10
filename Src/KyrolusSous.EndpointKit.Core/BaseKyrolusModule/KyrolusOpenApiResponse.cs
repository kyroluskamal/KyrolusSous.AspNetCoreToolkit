namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public sealed record KyrolusOpenApiResponse(
    int StatusCode,
    Type? ResponseType = null,
    string? ContentType = null);
