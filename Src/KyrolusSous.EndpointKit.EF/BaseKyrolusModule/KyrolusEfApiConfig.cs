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
    public IReadOnlyCollection<string>? AllowedPatchProperties { get; set; }
    public bool StrictFilterValidation { get; set; }
    public bool FilterCaseInsensitive { get; set; } = true;
    public bool StrictIncludeValidation { get; set; }
    public bool StrictSelectValidation { get; set; }
    public bool StrictPatchValidation { get; set; }
    public int MaxIncludeGraphDepth { get; set; } = 3;
    public int DefaultPageSize { get; set; } = 50;
    public int MaxPageSize { get; set; } = 200;
    public bool EnableQueryEndpoints { get; set; } = true;
    public bool EnablePagedEndpoints { get; set; } = true;
    public bool EnableSeekEndpoints { get; set; } = true;
    public bool EnableCompositeKeyEndpoints { get; set; } = true;
    public bool EnableBulkEndpoints { get; set; } = false;
    public int BulkChunkSize { get; set; } = 200;
    public bool CompositeKeyOnly { get; set; } = false;
    public string KeyPropertyName { get; set; } = "Id";
    public bool EnableSoftDeleteEndpoints { get; set; } = true;
    public bool UseSoftDeleteForDelete { get; set; } = true;
    public string? TenantPropertyName { get; set; } = "TenantId";
    public string? ScopePropertyName { get; set; } = "ScopeKey";
    public bool RequireTenant { get; set; } = false;
    public string? RowVersionPropertyName { get; set; } = "RowVersion";
    public bool EnableEtags { get; set; } = true;
    public IReadOnlyList<Type>? CompositeKeyTypes { get; set; }
    public IReadOnlyList<string>? CompositeKeyPropertyNames { get; set; }
    public Func<IReadOnlyList<string>, object?[]>? CompositeKeyParser { get; set; }
    public Action<TResponse, IReadOnlyList<object?>>? SetCompositeKey { get; set; }
    public IKyrolusQuery<TResponse?>? QueryByKeyValues { get; set; }
    public IKyrolusCommand<int>? ExecuteUpdateCommand { get; set; }
    public IKyrolusCommand<int>? ExecuteDeleteCommand { get; set; }
    public IKyrolusCommand<bool>? RestoreCommand { get; set; }
}
