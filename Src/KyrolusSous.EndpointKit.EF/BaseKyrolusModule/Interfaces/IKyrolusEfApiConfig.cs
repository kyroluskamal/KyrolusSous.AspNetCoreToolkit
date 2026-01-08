namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

public interface IKyrolusEfApiConfig<TResponse> : IKyrolusApiConfig<TResponse>
    where TResponse : class
{
    IReadOnlyCollection<string>? AllowedFilterProperties { get; set; }
    IReadOnlyCollection<string>? AllowedOrderProperties { get; set; }
    IReadOnlyCollection<string>? AllowedIncludeProperties { get; set; }
    bool StrictFilterValidation { get; set; }
    bool StrictIncludeValidation { get; set; }
    int DefaultPageSize { get; set; }
    int MaxPageSize { get; set; }
    bool EnableQueryEndpoints { get; set; }
    bool EnablePagedEndpoints { get; set; }
}
