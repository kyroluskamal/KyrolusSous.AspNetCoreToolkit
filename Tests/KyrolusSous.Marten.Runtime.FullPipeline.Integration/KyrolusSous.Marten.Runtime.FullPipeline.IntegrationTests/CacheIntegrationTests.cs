using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class CacheIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact(DisplayName = "Cache - get all menu items stores cache entry")]
    public async Task Menu_items_get_all_is_cached()
    {
        const string tenant = "tenant-cache";
        using var client = factory.CreateClientWithTenant(tenant);
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Cached Item",
            Category = "Main",
            Price = 75
        });

        var response = await client.GetAsync("/api/menu-items");
        response.EnsureSuccessStatusCode();

        var cache = factory.Services.GetRequiredService<ICacheProvider>();
        var cached = await cache.ExistsAsync(CacheKeys.MenuItemsAll(tenant));
        cached.ShouldBeTrue();
    }

    [Fact(DisplayName = "Cache - get menu item by id stores cache entry")]
    public async Task Menu_items_get_by_id_is_cached()
    {
        const string tenant = "tenant-cache-by-id";
        using var client = factory.CreateClientWithTenant(tenant);
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Cached By Id",
            Category = "Main",
            Price = 85
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var response = await client.GetAsync($"/api/menu-items/{created!.Id}");
        response.EnsureSuccessStatusCode();

        var cache = factory.Services.GetRequiredService<ICacheProvider>();
        var cached = await cache.ExistsAsync(CacheKeys.MenuItemById(tenant, created.Id));
        cached.ShouldBeTrue();
    }

    [Fact(DisplayName = "Cache - includeDeleted does not cache get-all results")]
    public async Task Menu_items_include_deleted_does_not_cache()
    {
        const string tenant = "tenant-cache-include-deleted";
        using var client = factory.CreateClientWithTenant(tenant);
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Soft Item",
            Category = "Main",
            Price = 30
        });

        var response = await client.GetAsync("/api/menu-items?includeDeleted=true");
        response.EnsureSuccessStatusCode();

        var cache = factory.Services.GetRequiredService<ICacheProvider>();
        var cached = await cache.ExistsAsync(CacheKeys.MenuItemsAll(tenant));
        cached.ShouldBeFalse();
    }

    [Fact(DisplayName = "Cache - by-id cache is tenant scoped")]
    public async Task Menu_item_by_id_cache_is_tenant_scoped()
    {
        const string tenantA = "tenant-cache-a";
        const string tenantB = "tenant-cache-b";
        using var clientA = factory.CreateClientWithTenant(tenantA);

        var createResponse = await clientA.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Tenant A Item",
            Category = "Main",
            Price = 45
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var response = await clientA.GetAsync($"/api/menu-items/{created!.Id}");
        response.EnsureSuccessStatusCode();

        var cache = factory.Services.GetRequiredService<ICacheProvider>();
        var cachedA = await cache.ExistsAsync(CacheKeys.MenuItemById(tenantA, created.Id));
        var cachedB = await cache.ExistsAsync(CacheKeys.MenuItemById(tenantB, created.Id));
        cachedA.ShouldBeTrue();
        cachedB.ShouldBeFalse();
    }

    [Fact(DisplayName = "Cache - filtered get-all does not cache results")]
    public async Task Filtered_get_all_does_not_cache()
    {
        const string tenant = "tenant-cache-filter";
        using var client = factory.CreateClientWithTenant(tenant);
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Cached Filter",
            Category = "Main",
            Price = 20
        });

        var filter = Uri.EscapeDataString("Name==\"Cached Filter\"");
        var response = await client.GetAsync($"/api/menu-items?filter={filter}");
        response.EnsureSuccessStatusCode();

        var cache = factory.Services.GetRequiredService<ICacheProvider>();
        var cached = await cache.ExistsAsync(CacheKeys.MenuItemsAll(tenant));
        cached.ShouldBeFalse();
    }

    [Fact(DisplayName = "Cache - get all cache is tenant scoped")]
    public async Task Get_all_cache_is_tenant_scoped()
    {
        var tenantA = TestHelpers.NewTenantId("cache-all-a");
        var tenantB = TestHelpers.NewTenantId("cache-all-b");
        using var clientA = factory.CreateClientWithTenant(tenantA);

        await clientA.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Tenant A Cached",
            Category = "Main",
            Price = 30
        });

        var response = await clientA.GetAsync("/api/menu-items");
        response.EnsureSuccessStatusCode();

        var cache = factory.Services.GetRequiredService<ICacheProvider>();
        var cachedA = await cache.ExistsAsync(CacheKeys.MenuItemsAll(tenantA));
        var cachedB = await cache.ExistsAsync(CacheKeys.MenuItemsAll(tenantB));
        cachedA.ShouldBeTrue();
        cachedB.ShouldBeFalse();
    }

    [Fact(DisplayName = "Cache - includeDeleted by id caches entry")]
    public async Task Include_deleted_by_id_caches_entry()
    {
        var tenant = TestHelpers.NewTenantId("cache-include-deleted");
        using var client = factory.CreateClientWithTenant(tenant);
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Soft Cached",
            Category = "Main",
            Price = 15
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{created!.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/menu-items/{created.Id}?includeDeleted=true");
        response.EnsureSuccessStatusCode();

        var cache = factory.Services.GetRequiredService<ICacheProvider>();
        var cached = await cache.ExistsAsync(CacheKeys.MenuItemById(tenant, created.Id));
        cached.ShouldBeTrue();
    }
}
