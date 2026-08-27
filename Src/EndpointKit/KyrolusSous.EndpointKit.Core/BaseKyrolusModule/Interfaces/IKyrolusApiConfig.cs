using KyrolusSous.Caching.Abstractions;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;

public interface IKyrolusEndpointConfig
{
    public EndpointNames Name { get; set; }
    public string[] IncludeProps { get; set; }
    public Type? ViewModelType { get; set; }
    public bool Authorize { get; set; }
    public dynamic? AuthorizationPolicy { get; set; }
    public string? RateLimitPolicy { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public bool? Idempotent { get; set; }
    public IReadOnlyCollection<KyrolusOpenApiResponse>? Responses { get; set; }
    public bool? OutputCacheEnabled { get; set; }
    public KyrolusCachePolicy? OutputCachePolicy { get; set; }
}

/// <summary>
/// Backward-compatibility alias for <see cref="IKyrolusEndpointConfig"/>.
/// </summary>
public interface IEndpointConfig : IKyrolusEndpointConfig
{
}

public interface IKyrolusApiConfig<TResponse>
where TResponse : class
{
    public string ApiName { get; set; }
    public string Prefix { get; set; }
    public string Route { get; set; }
    public string? ApiVersion { get; set; }
    public string VersionPrefix { get; set; }
    public bool AppendVersionToPrefix { get; set; }
    public string? RateLimitPolicy { get; set; }
    public bool EnableIdempotency { get; set; }
    public bool IdempotencyIncludeGet { get; set; }
    public string IdempotencyHeaderName { get; set; }
    public TimeSpan? IdempotencyTtl { get; set; }
    public bool EnableOutputCaching { get; set; }
    public KyrolusCachePolicy? OutputCachePolicy { get; set; }
    public IKyrolusQuery<TResponse?> QueryById { get; set; }
    public IKyrolusQuery<IEnumerable<TResponse>> QueryAll { get; set; }
    public IKyrolusQuery<IEnumerable<TResponse>> QueryByProperty { get; set; }
    public IKyrolusCommand<TResponse> AddCommand { get; set; }
    public IKyrolusCommand<IEnumerable<TResponse>> AddRangeCommand { get; set; }
    public IKyrolusCommand<TResponse> UpdateCommand { get; set; }
    public IKyrolusCommand<TResponse> PatchCommand { get; set; }
    public IKyrolusCommand<IEnumerable<TResponse>> UpdateRangeCommand { get; set; }
    public IKyrolusCommand RemoveCommand { get; set; }
    public IKyrolusCommand RemoveRangeCommand { get; set; }
    public IKyrolusCommand<bool> UpdateActiviationStateCommand { get; set; }
    public Func<TResponse, object?>? GetEntityId { get; set; }
    public Action<TResponse, object?>? SetEntityId { get; set; }

    public Type GetAllReturnType { get; set; }
    public Type GetByIdReturnType { get; set; }
    public Type AddReturnType { get; set; }
    public Type AddRangeReturnType { get; set; }
    public Type UpdateReturnType { get; set; }
    public Type UpdateRangeReturnType { get; set; }
    public Type RemoveReturnType { get; set; }
    public Type RemoveRangeReturnType { get; set; }
    public IEnumerable<IEndpointConfig> EndpointConfig { get; set; }
    public IReadOnlyCollection<KyrolusOpenApiResponse>? DefaultResponses { get; set; }
    public IEnumerable<EndpointNames> Endpoints { get; set; }
    public IEnumerable<EndpointNames> AllEndpointsExcept { get; set; }
    public Type ViewModelType { get; set; }
    public bool UseEnrichedCustomResponse { get; set; }
    public bool AuthorizeAllEndpoints { get; set; }
    public dynamic? GeneralAuthorizationPolicy { get; set; }

}
