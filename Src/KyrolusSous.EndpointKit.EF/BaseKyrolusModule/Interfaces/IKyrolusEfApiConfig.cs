namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

public interface IKyrolusEfApiConfig<TResponse> : IKyrolusApiConfig<TResponse>
    where TResponse : class
{
    IReadOnlyCollection<string>? AllowedFilterProperties { get; set; }
    IReadOnlyCollection<string>? AllowedOrderProperties { get; set; }
    IReadOnlyCollection<string>? AllowedIncludeProperties { get; set; }
    IReadOnlyCollection<string>? AllowedSelectProperties { get; set; }
    IReadOnlyCollection<string>? AllowedPatchProperties { get; set; }
    bool StrictFilterValidation { get; set; }
    bool FilterCaseInsensitive { get; set; }
    bool StrictIncludeValidation { get; set; }
    bool StrictSelectValidation { get; set; }
    bool StrictPatchValidation { get; set; }
    int MaxIncludeGraphDepth { get; set; }
    int DefaultPageSize { get; set; }
    int MaxPageSize { get; set; }
    bool EnableQueryEndpoints { get; set; }
    bool EnablePagedEndpoints { get; set; }
    bool EnableSeekEndpoints { get; set; }
    bool EnableCompositeKeyEndpoints { get; set; }
    bool EnableBulkEndpoints { get; set; }
    int BulkChunkSize { get; set; }
    bool CompositeKeyOnly { get; set; }
    string KeyPropertyName { get; set; }
    bool EnableSoftDeleteEndpoints { get; set; }
    bool UseSoftDeleteForDelete { get; set; }
    string? TenantPropertyName { get; set; }
    string? ScopePropertyName { get; set; }
    bool RequireTenant { get; set; }
    string? RowVersionPropertyName { get; set; }
    bool EnableEtags { get; set; }
    IReadOnlyList<Type>? CompositeKeyTypes { get; set; }
    IReadOnlyList<string>? CompositeKeyPropertyNames { get; set; }
    Func<IReadOnlyList<string>, object?[]>? CompositeKeyParser { get; set; }
    Action<TResponse, IReadOnlyList<object?>>? SetCompositeKey { get; set; }
    IKyrolusQuery<TResponse?>? QueryByKeyValues { get; set; }
    IKyrolusCommand<int>? ExecuteUpdateCommand { get; set; }
    IKyrolusCommand<int>? ExecuteDeleteCommand { get; set; }
    IKyrolusCommand<bool>? RestoreCommand { get; set; }
}
