using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public sealed class KyrolusEndpointConfig : IEndpointConfig
{
    public EndpointNames Name { get; set; }
    public string[] IncludeProps { get; set; } = [];
    public Type? ViewModelType { get; set; }
    public bool Authorize { get; set; }
    public dynamic? AuthorizationPolicy { get; set; }
    public string? RateLimitPolicy { get; set; }
    public bool? Idempotent { get; set; }
    public IReadOnlyCollection<KyrolusOpenApiResponse>? Responses { get; set; }
}
