using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.Repositories.EF.Abstractions.Policy;

public sealed class KyrolusRepositoryPolicy
{
    public bool? AsNoTrackingDefault { get; init; }
    public bool? UseSplitQueryDefault { get; init; }
    public bool? EnableSoftDeleteDefault { get; init; }
    public KyrolusDefaultIncludeMode DefaultIncludeMode { get; init; } = KyrolusDefaultIncludeMode.Merge;
    public Dictionary<Type, string[]> DefaultIncludeProperties { get; init; } = [];
    public IKyrolusRepositoryPolicyProvider? PolicyProvider { get; init; }
    public string? SoftDeleteProperty { get; init; } = "IsDeleted";
    public string? RowVersionProperty { get; init; }
    public Dictionary<Type, List<Delegate>> GlobalQueryFilters { get; init; } = [];
    public Dictionary<Type, KyrolusCachePolicy> CachePolicies { get; init; } = [];
    public Dictionary<Type, KyrolusCacheReadOperations> CacheReadOperations { get; init; } = [];
    public KyrolusCacheReadOperations DefaultCacheReadOperations { get; init; } = KyrolusCacheReadOperations.SafeDefaults;
    public KyrolusCachePolicy? DefaultCachePolicy { get; init; }
    public IKyrolusRepositoryCachePolicyProvider? CachePolicyProvider { get; init; }
    public int ConcurrencyRetryCount { get; init; } = 0;
    public TimeSpan? ConcurrencyRetryDelay { get; init; }
    public int? DefaultPageSize { get; init; }
    public static KyrolusRepositoryPolicy Default { get; } = new();
}
