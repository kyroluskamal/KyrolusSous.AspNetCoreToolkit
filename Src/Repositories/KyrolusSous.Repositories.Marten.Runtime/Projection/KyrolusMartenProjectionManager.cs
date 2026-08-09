namespace KyrolusSous.Repositories.Marten.Runtime.Projection;

public sealed class KyrolusMartenProjectionManager : IKyrolusMartenProjectionManager
{
    private readonly IKyrolusMartenProjectionOrchestrator orchestrator;
    private readonly IReadOnlyList<string> projectionNames;
    private readonly ILogger<KyrolusMartenProjectionManager>? logger;

    public KyrolusMartenProjectionManager(
        IDocumentStore store,
        IKyrolusMartenProjectionOrchestrator orchestrator,
        IEnumerable<string>? projectionNames = null,
        ILogger<KyrolusMartenProjectionManager>? logger = null)
    {
        _ = store ?? throw new ArgumentNullException(nameof(store));
        this.orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        this.logger = logger;
        this.projectionNames = NormalizeProjectionNames(projectionNames)
                                ?? DiscoverProjectionNames(store);
    }

    public Task RebuildAsync(CancellationToken cancellationToken = default)
        => ForEachProjectionAsync(orchestrator.EnqueueRebuildAsync, cancellationToken);

    public Task AssertIsUpToDateAsync(CancellationToken cancellationToken = default)
        => ForEachProjectionAsync(orchestrator.EnsureUpToDateAsync, cancellationToken);

    private async Task ForEachProjectionAsync(
        Func<string, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        if (projectionNames.Count == 0)
        {
            logger?.LogWarning("No projections were resolved. Skipping projection operation.");
            return;
        }

        foreach (var name in projectionNames)
        {
            await action(name, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<string>? NormalizeProjectionNames(IEnumerable<string>? projectionNames)
    {
        if (projectionNames is null) return null;
        return [.. projectionNames
            .Select(name => name?.Trim())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string[] DiscoverProjectionNames(IDocumentStore store)
    {
        var options = store.GetType().GetProperty("Options")?.GetValue(store);
        var projections = options?.GetType().GetProperty("Projections")?.GetValue(options);
        var all = projections?.GetType().GetProperty("All")?.GetValue(projections) as IEnumerable;
        if (all is null) return [];

        return [.. all.Cast<object>()
            .Select(ExtractProjectionName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ExtractProjectionName(object? projection)
    {
        if (projection is null) return null;

        var value = projection;
        var valueProp = projection.GetType().GetProperty("Value");
        if (valueProp is not null)
        {
            value = valueProp.GetValue(projection);
        }

        if (value is null) return null;
        var type = value.GetType();
        var nameProp = type.GetProperty("ProjectionName") ?? type.GetProperty("Name");
        if (nameProp?.GetValue(value) is string name) return name;
        return type.Name;
    }
}

public sealed class KyrolusMartenExplicitProjectionManager : IKyrolusMartenProjectionManager
{
    private readonly IKyrolusMartenProjectionOrchestrator orchestrator;
    private readonly IReadOnlyList<string> projectionNames;
    private readonly ILogger<KyrolusMartenExplicitProjectionManager>? logger;

    public KyrolusMartenExplicitProjectionManager(
        IKyrolusMartenProjectionOrchestrator orchestrator,
        IEnumerable<string> projectionNames,
        ILogger<KyrolusMartenExplicitProjectionManager>? logger = null)
    {
        this.orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        this.logger = logger;
        this.projectionNames = [.. projectionNames
            .Select(name => name?.Trim())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public Task RebuildAsync(CancellationToken cancellationToken = default)
        => ForEachProjectionAsync(orchestrator.EnqueueRebuildAsync, cancellationToken);

    public Task AssertIsUpToDateAsync(CancellationToken cancellationToken = default)
        => ForEachProjectionAsync(orchestrator.EnsureUpToDateAsync, cancellationToken);

    private async Task ForEachProjectionAsync(
        Func<string, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        if (projectionNames.Count == 0)
        {
            logger?.LogWarning("No projection names provided. Skipping projection operation.");
            return;
        }

        foreach (var name in projectionNames)
        {
            await action(name, cancellationToken).ConfigureAwait(false);
        }
    }
}
