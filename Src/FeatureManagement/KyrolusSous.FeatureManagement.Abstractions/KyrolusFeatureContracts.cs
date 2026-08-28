namespace KyrolusSous.FeatureManagement.Abstractions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusFeatureGateAttribute(string featureName) : Attribute
{
    public string FeatureName { get; } = featureName ?? throw new ArgumentNullException(nameof(featureName));
}

public sealed record KyrolusFeatureContext
{
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public IReadOnlyList<string>? Roles { get; init; }
    public IDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

public interface IKyrolusFeatureFilter
{
    string Name { get; }
    ValueTask<bool> EvaluateAsync(string featureName, KyrolusFeatureContext? context, IDictionary<string, string> parameters, CancellationToken cancellationToken = default);
}

public interface IKyrolusFeatureStore
{
    Task<bool?> GetFeatureStateAsync(string featureName, CancellationToken cancellationToken = default);
    Task SetFeatureStateAsync(string featureName, bool enabled, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, bool>> GetAllStatesAsync(CancellationToken cancellationToken = default);
}

public interface IKyrolusFeatureManager
{
    ValueTask<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default);
    ValueTask<bool> IsEnabledAsync(string featureName, KyrolusFeatureContext context, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GetFeatureNamesAsync(CancellationToken cancellationToken = default);
}
