using KyrolusSous.EasyAPI.BaseKyrolusModule.Enum;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

public interface IEndpointConfig
{
    public EndpointNames Name { get; set; }
    public string[] IncludeProps { get; set; }
    public Type? ViewModelType { get; set; }
    public bool Authorize { get; set; }
    public dynamic? AuthorizationPolicy { get; set; }

}

public interface IKyrolusApiConfig<TResponse>
where TResponse : class
{
    public string ApiName { get; set; }
    public string Prefix { get; set; }
    public string Route { get; set; }
    public IKyrolusQuery<TResponse?> QueryById { get; set; }
    public IKyrolusQuery<IEnumerable<TResponse>> QueryAll { get; set; }
    public IKyrolusQuery<IEnumerable<TResponse>> QueryByProperty { get; set; }
    public IKyrolusCommand<TResponse> AddCommand { get; set; }
    public IKyrolusCommand<IEnumerable<TResponse>> AddRangeCommand { get; set; }
    public IKyrolusCommand<TResponse> UpdateCommand { get; set; }
    public IKyrolusCommand<TResponse> PatchCommand { get; set; }
    public IKyrolusCommand<IEnumerable<TResponse>> UpdateRangeCommand { get; set; }
    public IKyrolusCommand<Unit> RemoveCommand { get; set; }
    public IKyrolusCommand<IEnumerable<Unit>> RemoveRangeCommand { get; set; }
    public IKyrolusCommand<bool> UpdateActiviationStateCommand { get; set; }

    public Type GetAllReturnType { get; set; }
    public Type GetByIdReturnType { get; set; }
    public Type AddReturnType { get; set; }
    public Type AddRangeReturnType { get; set; }
    public Type UpdateReturnType { get; set; }
    public Type UpdateRangeReturnType { get; set; }
    public Type RemoveReturnType { get; set; }
    public Type RemoveRangeReturnType { get; set; }
    public IEnumerable<IEndpointConfig> EndpointConfig { get; set; }
    public IEnumerable<EndpointNames> Endpoints { get; set; }
    public IEnumerable<EndpointNames> AllEndpointsExcept { get; set; }
    public Type ViewModelType { get; set; }
    public bool UseEnrichedCustomResponse { get; set; }
    public bool AuthorizeAllEndpoints { get; set; }
    public dynamic? GeneralAuthorizationPolicy { get; set; }

}
