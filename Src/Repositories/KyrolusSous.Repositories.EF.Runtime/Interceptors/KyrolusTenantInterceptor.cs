using KyrolusSous.Repositories.EF.Abstractions.MultiTenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KyrolusSous.Repositories.EF.Runtime.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that automatically assigns the ambient <see cref="ICurrentTenantContext.TenantId"/> to newly created entities.
/// </summary>
public sealed class KyrolusTenantInterceptor(ICurrentTenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyTenantId(eventData.Context);
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
            ApplyTenantId(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyTenantId(DbContext context)
    {
        var tenantId = tenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<IKyrolusTenantScopedEntity>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrWhiteSpace(entry.Entity.TenantId))
            {
                entry.Entity.TenantId = tenantId;
            }
        }
    }
}
