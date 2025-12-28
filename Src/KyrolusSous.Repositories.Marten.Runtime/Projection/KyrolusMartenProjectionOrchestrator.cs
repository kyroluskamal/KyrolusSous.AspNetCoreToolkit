namespace KyrolusSous.Repositories.Marten.Runtime.Projection;

/// <summary>
/// Generic projection orchestration using Marten's projection daemon via reflection
/// to stay resilient across Marten versions while remaining AOT-friendly.
/// </summary>
public sealed class KyrolusMartenProjectionOrchestrator : IKyrolusMartenProjectionOrchestrator
{
    private readonly IDocumentStore store;
    private readonly ILogger<KyrolusMartenProjectionOrchestrator>? logger;
    private readonly KyrolusMartenDaemonOptions daemonOptions;
    private object? daemon;

    public KyrolusMartenProjectionOrchestrator(
        IDocumentStore store,
        IOptions<KyrolusMartenDaemonOptions>? options = null,
        ILogger<KyrolusMartenProjectionOrchestrator>? logger = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.logger = logger;
        daemonOptions = options?.Value ?? new KyrolusMartenDaemonOptions();
    }

    public async Task EnqueueRebuildAsync(string projectionName, CancellationToken cancellationToken = default)
    {
        var d = await GetDaemonAsync().ConfigureAwait(false);
        var rebuild = (d.GetType().GetMethod("RebuildProjection", [typeof(string), typeof(CancellationToken)])
                     ?? d.GetType().GetMethod("RebuildProjection", [typeof(string)])) ?? throw new NotSupportedException("RebuildProjection not available on projection daemon.");

        var result = rebuild.GetParameters().Length == 2
            ? rebuild.Invoke(d, [projectionName, cancellationToken])
            : rebuild.Invoke(d, [projectionName]);

        if (result is Task task) await task.ConfigureAwait(false);
    }

    public async Task ApplyEventAsync(object @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        using var session = store.LightweightSession();
        session.Events.Append(Guid.NewGuid(), @event);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureUpToDateAsync(string projectionName, CancellationToken cancellationToken = default)
    {
        var d = await GetDaemonAsync().ConfigureAwait(false);
        var timeout = daemonOptions.WaitForNonStaleTimeout;
        var token = cancellationToken;
        if (timeout.HasValue)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout.Value);
            token = cts.Token;
        }

        var waitMethod = d.GetType().GetMethod("WaitForNonStaleData", new[] { typeof(CancellationToken) })
                       ?? d.GetType().GetMethod("WaitForNonStaleData");

        if (waitMethod != null)
        {
            var result = waitMethod.GetParameters().Length == 1
                ? waitMethod.Invoke(d, [token])
                : waitMethod.Invoke(d, []);
            if (result is Task task) await task.ConfigureAwait(false);
            return;
        }

        logger?.LogInformation("Projection daemon does not expose WaitForNonStaleData; skipping freshness check for {Projection}", projectionName);
    }

    private async Task<object> GetDaemonAsync()
    {
        if (daemon != null) return daemon;

        var settings = CreateDaemonSettings();
        daemon = await BuildDaemonAsync(settings).ConfigureAwait(false);
        await StartDaemonAsync(daemon).ConfigureAwait(false);
        await RebuildIfRequestedAsync(daemon).ConfigureAwait(false);
        return daemon!;
    }

    private object? CreateDaemonSettings()
    {
        var settingsType = Type.GetType("Marten.Events.Daemon.DaemonSettings, Marten")
                         ?? Type.GetType("Marten.Events.Daemon.DaemonSettings, Marten.AsyncDaemon");
        if (settingsType == null) return null;
        var instance = Activator.CreateInstance(settingsType);
        if (instance != null)
        {
            daemonOptions.ConfigureSettings?.Invoke(instance);
        }
        return instance;
    }

    private async Task<object> BuildDaemonAsync(object? settings)
    {
        var storeType = store.GetType();
        var methods = storeType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name is "BuildProjectionDaemonAsync" or "BuildProjectionDaemon").ToList();

        MethodInfo? selected = null;
        object?[]? args = null;

        if (settings != null)
        {
            selected = methods.FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Length == 1 && p[0].ParameterType.IsInstanceOfType(settings);
            });
            if (selected != null) args = [settings];
        }

        selected ??= methods.FirstOrDefault(m => m.GetParameters().Length == 0)
                  ?? throw new NotSupportedException("Projection daemon factory not found on IDocumentStore.");

        var result = selected.Invoke(store, args);
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            return ((dynamic)task).Result;
        }

        return result ?? throw new InvalidOperationException("Failed to create projection daemon.");
    }

    private async Task StartDaemonAsync(object daemonInstance)
    {
        if (!daemonOptions.AutoStart) return;

        var startShard = daemonInstance.GetType().GetMethod("StartShard");
        var startAll = daemonInstance.GetType().GetMethod("StartAllShards", Type.EmptyTypes)
                      ?? daemonInstance.GetType().GetMethod("StartAllShards");

        if (daemonOptions.ShardsToStart is { Count: > 0 } && startShard != null)
        {
            await StartSpecificShardsAsync(daemonInstance, startShard, daemonOptions.ShardsToStart).ConfigureAwait(false);
            return;
        }

        if (startAll != null)
        {
            await InvokePossiblyAsync(startAll, daemonInstance, startAll.GetParameters().Length == 0
                ? []
                : new object[] { CancellationToken.None }).ConfigureAwait(false);
        }
    }

    private static async Task StartSpecificShardsAsync(object daemonInstance, MethodInfo startShard, IReadOnlyList<string> shards)
    {
        foreach (var shardName in shards)
        {
            var shardArg = BuildShardArgument(startShard, shardName);
            var parameters = startShard.GetParameters().Length == 1
                ? [shardArg]
                : new object?[] { shardArg, CancellationToken.None };

            await InvokePossiblyAsync(startShard, daemonInstance, parameters).ConfigureAwait(false);
        }
    }

    private static object? BuildShardArgument(MethodInfo startShard, string shardName)
    {
        var shardParam = startShard.GetParameters().FirstOrDefault();
        if (shardParam?.ParameterType == typeof(string) || shardParam?.ParameterType == null)
        {
            return shardName;
        }

        var shardType = shardParam.ParameterType;
        var ctor = shardType.GetConstructor([typeof(string)]);
        return ctor != null ? ctor.Invoke([shardName]) : shardName;
    }

    private static async Task InvokePossiblyAsync(MethodInfo method, object target, object?[] args)
    {
        var result = method.Invoke(target, args);
        if (result is Task task) await task.ConfigureAwait(false);
    }

    private async Task RebuildIfRequestedAsync(object daemonInstance)
    {
        if (daemonOptions.RebuildProjections is not { Count: > 0 }) return;
        var rebuild = daemonInstance.GetType().GetMethod("RebuildProjection");
        if (rebuild == null) return;

        foreach (var projection in daemonOptions.RebuildProjections)
        {
            var res = rebuild.GetParameters().Length switch
            {
                1 => rebuild.Invoke(daemonInstance, [projection]),
                2 => rebuild.Invoke(daemonInstance, [projection, CancellationToken.None]),
                _ => rebuild.Invoke(daemonInstance, [projection])
            };
            if (res is Task t) await t.ConfigureAwait(false);
        }
    }
}
