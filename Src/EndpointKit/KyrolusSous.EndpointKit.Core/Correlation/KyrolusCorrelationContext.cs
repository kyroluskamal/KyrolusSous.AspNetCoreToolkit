using System.Diagnostics;

namespace KyrolusSous.EndpointKit.Core.Correlation;

/// <summary>
/// Ambient async-local context holding correlation, tracing, and multi-tenant identifiers.
/// Decoupled from logging and available to all inbound and CQRS pipeline components.
/// </summary>
public static class KyrolusCorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();
    private static readonly AsyncLocal<string?> CurrentTenantId = new();
    private static readonly AsyncLocal<string?> CurrentUserId = new();

    /// <summary>
    /// Gets or sets the current ambient Correlation ID.
    /// If not explicitly set, falls back to <see cref="Activity.Current"/> TraceId or null.
    /// </summary>
    public static string? CorrelationId
    {
        get => CurrentCorrelationId.Value ?? Activity.Current?.TraceId.ToString();
        set => CurrentCorrelationId.Value = value;
    }

    /// <summary>
    /// Gets or sets the current ambient Tenant ID.
    /// </summary>
    public static string? TenantId
    {
        get => CurrentTenantId.Value;
        set => CurrentTenantId.Value = value;
    }

    /// <summary>
    /// Gets or sets the current ambient User ID.
    /// </summary>
    public static string? UserId
    {
        get => CurrentUserId.Value;
        set => CurrentUserId.Value = value;
    }

    /// <summary>
    /// Begins a correlation scope, restoring previous values upon disposal.
    /// </summary>
    public static IDisposable BeginScope(string? correlationId = null, string? tenantId = null, string? userId = null)
    {
        var prevCorrelation = CurrentCorrelationId.Value;
        var prevTenant = CurrentTenantId.Value;
        var prevUser = CurrentUserId.Value;

        if (correlationId is not null)
        {
            CurrentCorrelationId.Value = correlationId;
        }

        if (tenantId is not null)
        {
            CurrentTenantId.Value = tenantId;
        }

        if (userId is not null)
        {
            CurrentUserId.Value = userId;
        }

        return new ScopeRestorer(prevCorrelation, prevTenant, prevUser);
    }

    private sealed class ScopeRestorer(string? prevCorrelation, string? prevTenant, string? prevUser) : IDisposable
    {
        public void Dispose()
        {
            CurrentCorrelationId.Value = prevCorrelation;
            CurrentTenantId.Value = prevTenant;
            CurrentUserId.Value = prevUser;
        }
    }
}
