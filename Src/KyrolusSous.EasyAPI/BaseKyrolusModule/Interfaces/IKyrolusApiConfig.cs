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
    public IQuery<TResponse> QueryById { get; set; }
    public IQuery<IEnumerable<TResponse>> QueryAll { get; set; }
    public IQuery<IEnumerable<TResponse>> QueryByProperty { get; set; }
    public ICommand<TResponse> AddCommand { get; set; }
    public ICommand<IEnumerable<TResponse>> AddRangeCommand { get; set; }
    public ICommand<TResponse> UpdateCommand { get; set; }
    public ICommand<TResponse> PatchCommand { get; set; }
    public ICommand<IEnumerable<TResponse>> UpdateRangeCommand { get; set; }
    public ICommand<Unit> RemoveCommand { get; set; }
    public ICommand<IEnumerable<Unit>> RemoveRangeCommand { get; set; }
    public ICommand<bool> UpdateActiviationStateCommand { get; set; }

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
