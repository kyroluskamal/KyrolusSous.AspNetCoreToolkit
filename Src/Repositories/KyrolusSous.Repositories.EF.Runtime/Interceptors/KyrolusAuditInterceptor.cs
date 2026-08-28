using KyrolusSous.Repositories.EF.Abstractions.Auditing;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KyrolusSous.Repositories.EF.Runtime.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that automatically populates audit metadata and tracks property changes.
/// </summary>
public sealed class KyrolusAuditInterceptor(
    IKyrolusCurrentUserContext? userContext = null,
    Action<IReadOnlyList<KyrolusAuditEntry>>? onAuditEntriesCaptured = null)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyAuditStamps(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ApplyAuditStamps(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditStamps(DbContext context)
    {
        var now = DateTime.UtcNow;
        var userId = userContext?.UserId;
        var auditEntries = new List<KyrolusAuditEntry>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is IKyrolusAuditableEntity auditable)
                {
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedBy = userId;
                }

                if (onAuditEntriesCaptured is not null)
                {
                    auditEntries.Add(CreateAuditEntry(entry, "Insert", userId, now));
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is IKyrolusAuditableEntity auditable)
                {
                    auditable.LastModifiedAtUtc = now;
                    auditable.LastModifiedBy = userId;
                }

                if (onAuditEntriesCaptured is not null)
                {
                    auditEntries.Add(CreateAuditEntry(entry, "Update", userId, now));
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                if (entry.Entity is IKyrolusFullAuditableEntity fullAuditable)
                {
                    // Convert hard delete to soft delete automatically if desired
                    entry.State = EntityState.Modified;
                    fullAuditable.IsDeleted = true;
                    fullAuditable.DeletedAtUtc = now;
                    fullAuditable.DeletedBy = userId;
                }

                if (onAuditEntriesCaptured is not null)
                {
                    auditEntries.Add(CreateAuditEntry(entry, "Delete", userId, now));
                }
            }
        }

        if (auditEntries.Count > 0 && onAuditEntriesCaptured is not null)
        {
            onAuditEntriesCaptured(auditEntries);
        }
    }

    private static KyrolusAuditEntry CreateAuditEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string action,
        string? userId,
        DateTime timestampUtc)
    {
        var audit = new KyrolusAuditEntry
        {
            EntityName = entry.Metadata.ClrType.Name,
            Action = action,
            UserId = userId,
            TimestampUtc = timestampUtc
        };

        foreach (var prop in entry.Properties)
        {
            var propName = prop.Metadata.Name;

            if (prop.Metadata.IsPrimaryKey())
            {
                audit.KeyValues[propName] = prop.CurrentValue;
                continue;
            }

            if (action == "Insert")
            {
                audit.NewValues[propName] = prop.CurrentValue;
            }
            else if (action == "Delete")
            {
                audit.OldValues[propName] = prop.OriginalValue;
            }
            else if (action == "Update" && prop.IsModified)
            {
                audit.OldValues[propName] = prop.OriginalValue;
                audit.NewValues[propName] = prop.CurrentValue;
                audit.ChangedColumns.Add(propName);
            }
        }

        return audit;
    }
}
