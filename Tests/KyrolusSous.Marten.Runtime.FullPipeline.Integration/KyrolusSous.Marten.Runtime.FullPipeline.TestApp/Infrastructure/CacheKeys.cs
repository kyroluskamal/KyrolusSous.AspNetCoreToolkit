namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static class CacheKeys
{
    public static string MenuItemsAll(string tenantId) => $"menu-items:tenant={tenantId}";
    public static string MenuItemById(string tenantId, Guid id) => $"menu-item:{id}:tenant={tenantId}";
}
