using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class KyrolusEfApiConfig<TResponse> : ApiKyrolusApiConfig<TResponse>, IKyrolusEfApiConfig<TResponse>
    where TResponse : class
{
    public IReadOnlyCollection<string>? AllowedFilterProperties { get; set; }
    public IReadOnlyCollection<string>? AllowedOrderProperties { get; set; }
    public IReadOnlyCollection<string>? AllowedIncludeProperties { get; set; }
    public bool StrictFilterValidation { get; set; }
    public bool StrictIncludeValidation { get; set; }
    public int DefaultPageSize { get; set; } = 50;
    public int MaxPageSize { get; set; } = 200;
    public bool EnableQueryEndpoints { get; set; } = true;
    public bool EnablePagedEndpoints { get; set; } = true;
}
