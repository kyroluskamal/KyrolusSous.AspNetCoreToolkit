namespace KyrolusSous.Repositories.EF.Abstractions.Policy;

public sealed class KyrolusRepositoryPolicy
{
    public bool? AsNoTrackingDefault { get; init; }
    public bool? UseSplitQueryDefault { get; init; }
    public bool? EnableSoftDeleteDefault { get; init; }
    public string? SoftDeleteProperty { get; init; } = "IsDeleted";
    public string? RowVersionProperty { get; init; }
    public Dictionary<Type, List<Delegate>> GlobalQueryFilters { get; init; } = [];
    public int ConcurrencyRetryCount { get; init; } = 0;
    public TimeSpan? ConcurrencyRetryDelay { get; init; }
    public int? DefaultPageSize { get; init; }
    public static KyrolusRepositoryPolicy Default { get; } = new();
}
