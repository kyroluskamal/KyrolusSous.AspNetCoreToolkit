using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class KyrolusEfApiConfig<TResponse> : ApiKyrolusApiConfig<TResponse>, IKyrolusEfApiConfig<TResponse>
    where TResponse : class
{
    public IReadOnlyCollection<string>? AllowedFilterProperties { get; set; }
    public IReadOnlyCollection<string>? AllowedOrderProperties { get; set; }
    public IReadOnlyCollection<string>? AllowedIncludeProperties { get; set; }
    public IReadOnlyCollection<string>? AllowedSelectProperties { get; set; }
    public bool StrictFilterValidation { get; set; }
    public bool StrictIncludeValidation { get; set; }
    public bool StrictSelectValidation { get; set; }
    public int DefaultPageSize { get; set; } = 50;
    public int MaxPageSize { get; set; } = 200;
    public bool EnableQueryEndpoints { get; set; } = true;
    public bool EnablePagedEndpoints { get; set; } = true;
    public bool EnableCompositeKeyEndpoints { get; set; } = true;
    public bool EnableBulkEndpoints { get; set; } = false;
    public bool CompositeKeyOnly { get; set; } = false;
    public string KeyPropertyName { get; set; } = "Id";
    public IReadOnlyList<Type>? CompositeKeyTypes { get; set; }
    public IReadOnlyList<string>? CompositeKeyPropertyNames { get; set; }
    public Func<IReadOnlyList<string>, object?[]>? CompositeKeyParser { get; set; }
    public Action<TResponse, IReadOnlyList<object?>>? SetCompositeKey { get; set; }
    public IKyrolusQuery<TResponse?>? QueryByKeyValues { get; set; }
    public IKyrolusCommand<int>? ExecuteUpdateCommand { get; set; }
    public IKyrolusCommand<int>? ExecuteDeleteCommand { get; set; }
}
