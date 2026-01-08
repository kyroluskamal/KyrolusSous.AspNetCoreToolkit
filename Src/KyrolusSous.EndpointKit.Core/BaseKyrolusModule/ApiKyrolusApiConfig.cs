namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public class ApiKyrolusApiConfig<TResponse> : IKyrolusApiConfig<TResponse>
    where TResponse : class
{
    public string ApiName { get; set; } = default!;
    public string Prefix { get; set; } = "api";
    public string Route { get; set; } = default!;
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
    public Type GetAllReturnType { get; set; } = default!;
    public Type GetByIdReturnType { get; set; } = default!;
    public Type AddReturnType { get; set; } = default!;
    public Type AddRangeReturnType { get; set; } = default!;
    public Type UpdateReturnType { get; set; } = default!;
    public Type UpdateRangeReturnType { get; set; } = default!;
    public Type RemoveReturnType { get; set; } = default!;
    public Type RemoveRangeReturnType { get; set; } = default!;
    public IEnumerable<EndpointNames> AllEndpointsExcept { get; set; } = default!;
    public IEnumerable<EndpointNames> Endpoints { get; set; } = [EndpointNames.All];
    public Type ViewModelType { get; set; } = default!;
    public bool UseEnrichedCustomResponse { get; set; } = true;
    public IEnumerable<IEndpointConfig> EndpointConfig { get; set; } = [];
    public bool AuthorizeAllEndpoints { get; set; } = false;
    public dynamic? GeneralAuthorizationPolicy { get; set; }
    public IKyrolusCommand<TResponse> PatchCommand { get; set; } = default!;
}
