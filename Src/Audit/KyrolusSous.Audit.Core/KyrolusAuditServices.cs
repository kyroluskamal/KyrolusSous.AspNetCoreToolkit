using System.Collections.Concurrent;
using System.Reflection;
using KyrolusSous.Audit.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Audit.Core;

public sealed class KyrolusInMemoryAuditStore : IKyrolusAuditStore
{
    private readonly ConcurrentBag<KyrolusAuditEntry> _entries = [];

    public Task SaveAuditEntriesAsync(IEnumerable<KyrolusAuditEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
        {
            _entries.Add(entry);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KyrolusAuditEntry>> GetEntityHistoryAsync(string entityName, string entityId, CancellationToken cancellationToken = default)
    {
        var result = _entries
            .Where(e => string.Equals(e.EntityName, entityName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(e.EntityId, entityId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.TimestampUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<KyrolusAuditEntry>>(result);
    }

    public Task<IReadOnlyList<KyrolusAuditEntry>> GetUserActivityAsync(string userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var result = _entries
            .Where(e => string.Equals(e.UserId, userId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<KyrolusAuditEntry>>(result);
    }
}

public sealed class KyrolusAuditDbContextInterceptor(
    IKyrolusAuditStore auditStore,
    IKyrolusAuditContextProvider? contextProvider = null,
    ILogger<KyrolusAuditDbContextInterceptor>? logger = null) : SaveChangesInterceptor
{
    private readonly IKyrolusAuditStore _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
    private readonly IKyrolusAuditContextProvider? _contextProvider = contextProvider;
    private readonly ILogger<KyrolusAuditDbContextInterceptor>? _logger = logger;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
        }

        var auditEntries = CreateAuditEntries(eventData.Context);
        if (auditEntries.Count > 0)
        {
            try
            {
                await _auditStore.SaveAuditEntriesAsync(auditEntries, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist {Count} audit entries.", auditEntries.Count);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private List<KyrolusAuditEntry> CreateAuditEntries(DbContext context)
    {
        var entries = new List<KyrolusAuditEntry>();
        var modifiedEntries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        var userId = _contextProvider?.GetCurrentUserId();
        var userName = _contextProvider?.GetCurrentUserName();
        var tenantId = _contextProvider?.GetCurrentTenantId();
        var ip = _contextProvider?.GetCurrentIpAddress();
        var userAgent = _contextProvider?.GetCurrentUserAgent();

        foreach (var entry in modifiedEntries)
        {
            var entityType = entry.Entity.GetType();
            var isAuditable = entityType.GetCustomAttribute<KyrolusAuditableAttribute>() is not null;
            if (!isAuditable && !entry.Properties.Any(p => p.Metadata.PropertyInfo?.GetCustomAttribute<KyrolusAuditableAttribute>() != null))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => KyrolusAuditAction.Create,
                EntityState.Modified => KyrolusAuditAction.Update,
                EntityState.Deleted => KyrolusAuditAction.Delete,
                _ => KyrolusAuditAction.Update
            };

            var changes = new List<KyrolusPropertyChange>();
            var primaryKey = string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString() ?? "0"));

            foreach (var prop in entry.Properties)
            {
                if (prop.Metadata.PropertyInfo?.GetCustomAttribute<KyrolusAuditIgnoreAttribute>() is not null)
                {
                    continue;
                }

                if (entry.State == EntityState.Added)
                {
                    changes.Add(new KyrolusPropertyChange
                    {
                        PropertyName = prop.Metadata.Name,
                        OriginalValue = null,
                        NewValue = prop.CurrentValue
                    });
                }
                else if (entry.State == EntityState.Deleted)
                {
                    changes.Add(new KyrolusPropertyChange
                    {
                        PropertyName = prop.Metadata.Name,
                        OriginalValue = prop.OriginalValue,
                        NewValue = null
                    });
                }
                else if (entry.State == EntityState.Modified && prop.IsModified)
                {
                    changes.Add(new KyrolusPropertyChange
                    {
                        PropertyName = prop.Metadata.Name,
                        OriginalValue = prop.OriginalValue,
                        NewValue = prop.CurrentValue
                    });
                }
            }

            entries.Add(new KyrolusAuditEntry
            {
                EntityName = entityType.Name,
                EntityId = primaryKey,
                Action = action,
                UserId = userId,
                UserName = userName,
                TenantId = tenantId,
                IpAddress = ip,
                UserAgent = userAgent,
                TimestampUtc = DateTimeOffset.UtcNow,
                Changes = changes
            });
        }

        return entries;
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusAudit(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusAuditStore, KyrolusInMemoryAuditStore>();
        services.AddScoped<KyrolusAuditDbContextInterceptor>();
        return services;
    }
}
