using KyrolusSous.Caching.Abstractions;
using System;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public class ApiKyrolusApiConfig<TResponse> : IKyrolusApiConfig<TResponse>
    where TResponse : class
{
    public string ApiName { get; set; } = default!;
    public string Prefix { get; set; } = "api";
    public string Route { get; set; } = default!;
    public string? ApiVersion { get; set; }
    public string VersionPrefix { get; set; } = "v";
    public bool AppendVersionToPrefix { get; set; } = true;
    public string? RateLimitPolicy { get; set; }
    public bool EnableIdempotency { get; set; } = false;
    public bool IdempotencyIncludeGet { get; set; } = false;
    public string IdempotencyHeaderName { get; set; } = "Idempotency-Key";
    public TimeSpan? IdempotencyTtl { get; set; } = TimeSpan.FromMinutes(10);
    public bool EnableOutputCaching { get; set; } = false;
    public KyrolusCachePolicy? OutputCachePolicy { get; set; }
    public IKyrolusQuery<TResponse?> QueryById { get; set; } = default!;
    public IKyrolusQuery<IEnumerable<TResponse>> QueryAll { get; set; } = default!;
    public IKyrolusQuery<IEnumerable<TResponse>> QueryByProperty { get; set; } = default!;
    public IKyrolusCommand<TResponse> AddCommand { get; set; } = default!;
    public IKyrolusCommand<IEnumerable<TResponse>> AddRangeCommand { get; set; } = default!;
    public IKyrolusCommand<TResponse> UpdateCommand { get; set; } = default!;
    public IKyrolusCommand<IEnumerable<TResponse>> UpdateRangeCommand { get; set; } = default!;
    public IKyrolusCommand RemoveCommand { get; set; } = default!;
    public IKyrolusCommand<bool> UpdateActiviationStateCommand { get; set; } = default!;
    public IKyrolusCommand RemoveRangeCommand { get; set; } = default!;
    public Func<TResponse, object?>? GetEntityId { get; set; }
    public Action<TResponse, object?>? SetEntityId { get; set; }
    public Type GetAllReturnType { get; set; } = typeof(IEnumerable<TResponse>);
    public Type GetByIdReturnType { get; set; } = typeof(TResponse);
    public Type AddReturnType { get; set; } = typeof(TResponse);
    public Type AddRangeReturnType { get; set; } = typeof(IEnumerable<TResponse>);
    public Type UpdateReturnType { get; set; } = typeof(TResponse);
    public Type UpdateRangeReturnType { get; set; } = typeof(IEnumerable<TResponse>);
    public Type RemoveReturnType { get; set; } = typeof(bool);
    public Type RemoveRangeReturnType { get; set; } = typeof(bool);
    public IEnumerable<EndpointNames> AllEndpointsExcept { get; set; } = [];
    public IEnumerable<EndpointNames> Endpoints { get; set; } = [EndpointNames.All];
    public Type ViewModelType { get; set; } = typeof(TResponse);
    public bool UseEnrichedCustomResponse { get; set; } = true;
    public IEnumerable<IEndpointConfig> EndpointConfig { get; set; } = [];
    public IReadOnlyCollection<KyrolusOpenApiResponse>? DefaultResponses { get; set; } = [];
    public bool AuthorizeAllEndpoints { get; set; } = false;
    public dynamic? GeneralAuthorizationPolicy { get; set; }
    public IKyrolusCommand<TResponse> PatchCommand { get; set; } = default!;
}
