using System.Security.Cryptography;
using System.Text;
using KyrolusSous.FeatureManagement.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.FeatureManagement.Core;

public sealed class KyrolusFeatureDefinition
{
    public bool Enabled { get; set; } = true;
    public string? FilterName { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class KyrolusFeatureOptions
{
    public Dictionary<string, KyrolusFeatureDefinition> Features { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class KyrolusPercentageFeatureFilter : IKyrolusFeatureFilter
{
    public string Name => "Percentage";

    public ValueTask<bool> EvaluateAsync(string featureName, KyrolusFeatureContext? context, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        if (!parameters.TryGetValue("Value", out var percentageStr) || !int.TryParse(percentageStr, out var percentage))
        {
            return ValueTask.FromResult(false);
        }

        if (percentage <= 0) return ValueTask.FromResult(false);
        if (percentage >= 100) return ValueTask.FromResult(true);

        var key = context?.UserId ?? context?.TenantId ?? featureName;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var value = BitConverter.ToUInt32(hash, 0) % 100;
        return ValueTask.FromResult(value < percentage);
    }
}

public sealed class KyrolusTenantFeatureFilter : IKyrolusFeatureFilter
{
    public string Name => "Tenant";

    public ValueTask<bool> EvaluateAsync(string featureName, KyrolusFeatureContext? context, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context?.TenantId) || !parameters.TryGetValue("AllowedTenants", out var tenantsStr))
        {
            return ValueTask.FromResult(false);
        }

        var allowedTenants = tenantsStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var isAllowed = allowedTenants.Contains(context.TenantId, StringComparer.OrdinalIgnoreCase);
        return ValueTask.FromResult(isAllowed);
    }
}

public sealed class KyrolusTimeWindowFeatureFilter : IKyrolusFeatureFilter
{
    public string Name => "TimeWindow";

    public ValueTask<bool> EvaluateAsync(string featureName, KyrolusFeatureContext? context, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (parameters.TryGetValue("Start", out var startStr) && DateTimeOffset.TryParse(startStr, out var start) && now < start)
        {
            return ValueTask.FromResult(false);
        }

        if (parameters.TryGetValue("End", out var endStr) && DateTimeOffset.TryParse(endStr, out var end) && now > end)
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(true);
    }
}

public sealed class KyrolusRoleFeatureFilter : IKyrolusFeatureFilter
{
    public string Name => "Role";

    public ValueTask<bool> EvaluateAsync(string featureName, KyrolusFeatureContext? context, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        if (context?.Roles is null || context.Roles.Count == 0 || !parameters.TryGetValue("AllowedRoles", out var rolesStr))
        {
            return ValueTask.FromResult(false);
        }

        var allowedRoles = rolesStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var isAllowed = context.Roles.Any(r => allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
        return ValueTask.FromResult(isAllowed);
    }
}

public sealed class KyrolusAppVersionFeatureFilter : IKyrolusFeatureFilter
{
    public string Name => "AppVersion";

    public ValueTask<bool> EvaluateAsync(string featureName, KyrolusFeatureContext? context, IDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        if (context is null || !context.Properties.TryGetValue("AppVersion", out var versionObj) || versionObj is null)
        {
            return ValueTask.FromResult(false);
        }

        if (!Version.TryParse(versionObj.ToString(), out var clientVersion))
        {
            return ValueTask.FromResult(false);
        }

        if (parameters.TryGetValue("MinVersion", out var minVerStr) && Version.TryParse(minVerStr, out var minVer) && clientVersion < minVer)
        {
            return ValueTask.FromResult(false);
        }

        if (parameters.TryGetValue("MaxVersion", out var maxVerStr) && Version.TryParse(maxVerStr, out var maxVer) && clientVersion > maxVer)
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(true);
    }
}

public sealed class KyrolusInMemoryFeatureStore : IKyrolusFeatureStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool?> GetFeatureStateAsync(string featureName, CancellationToken cancellationToken = default)
    {
        if (_states.TryGetValue(featureName, out var state))
        {
            return Task.FromResult<bool?>(state);
        }
        return Task.FromResult<bool?>(null);
    }

    public Task SetFeatureStateAsync(string featureName, bool enabled, CancellationToken cancellationToken = default)
    {
        _states[featureName] = enabled;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, bool>> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, bool>>(_states.ToDictionary(k => k.Key, v => v.Value));
    }
}

public sealed class KyrolusFeatureManager : IKyrolusFeatureManager
{
    private readonly KyrolusFeatureOptions _options;
    private readonly Dictionary<string, IKyrolusFeatureFilter> _filters;
    private readonly IKyrolusFeatureStore? _featureStore;

    public KyrolusFeatureManager(
        IOptions<KyrolusFeatureOptions> options,
        IEnumerable<IKyrolusFeatureFilter> filters,
        IKyrolusFeatureStore? featureStore = null)
    {
        _options = options?.Value ?? new KyrolusFeatureOptions();
        _filters = filters.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
        _featureStore = featureStore;
    }

    public ValueTask<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
        => IsEnabledAsync(featureName, new KyrolusFeatureContext(), cancellationToken);

    public async ValueTask<bool> IsEnabledAsync(string featureName, KyrolusFeatureContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);

        if (_featureStore is not null)
        {
            var dynamicState = await _featureStore.GetFeatureStateAsync(featureName, cancellationToken).ConfigureAwait(false);
            if (dynamicState.HasValue)
            {
                return dynamicState.Value;
            }
        }

        if (!_options.Features.TryGetValue(featureName, out var definition) || !definition.Enabled)
        {
            return false;
        }

        if (string.IsNullOrEmpty(definition.FilterName))
        {
            return true;
        }

        if (_filters.TryGetValue(definition.FilterName, out var filter))
        {
            return await filter.EvaluateAsync(featureName, context, definition.Parameters, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    public async IAsyncEnumerable<string> GetFeatureNamesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in _options.Features.Keys)
        {
            yield return key;
        }
        await Task.CompletedTask;
    }
}

public sealed class KyrolusFeatureGateEndpointFilter(string featureName, int requirementStatusCode = 404) : Microsoft.AspNetCore.Http.IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context, Microsoft.AspNetCore.Http.EndpointFilterDelegate next)
    {
        var manager = context.HttpContext.RequestServices.GetRequiredService<IKyrolusFeatureManager>();
        var isEnabled = await manager.IsEnabledAsync(featureName, context.HttpContext.RequestAborted).ConfigureAwait(false);

        if (!isEnabled)
        {
            return Microsoft.AspNetCore.Http.Results.StatusCode(requirementStatusCode);
        }

        return await next(context).ConfigureAwait(false);
    }
}

public static class RouteHandlerBuilderExtensions
{
    public static Microsoft.AspNetCore.Builder.RouteHandlerBuilder RequireFeature(
        this Microsoft.AspNetCore.Builder.RouteHandlerBuilder builder,
        string featureName,
        int requirementStatusCode = 404)
    {
        return builder.AddEndpointFilter(new KyrolusFeatureGateEndpointFilter(featureName, requirementStatusCode));
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFeatureManagement(this IServiceCollection services, Action<KyrolusFeatureOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IKyrolusFeatureFilter, KyrolusPercentageFeatureFilter>();
        services.AddSingleton<IKyrolusFeatureFilter, KyrolusTenantFeatureFilter>();
        services.AddSingleton<IKyrolusFeatureFilter, KyrolusTimeWindowFeatureFilter>();
        services.AddSingleton<IKyrolusFeatureFilter, KyrolusRoleFeatureFilter>();
        services.AddSingleton<IKyrolusFeatureFilter, KyrolusAppVersionFeatureFilter>();
        services.AddSingleton<IKyrolusFeatureStore, KyrolusInMemoryFeatureStore>();
        services.AddSingleton<IKyrolusFeatureManager, KyrolusFeatureManager>();
        return services;
    }
}
