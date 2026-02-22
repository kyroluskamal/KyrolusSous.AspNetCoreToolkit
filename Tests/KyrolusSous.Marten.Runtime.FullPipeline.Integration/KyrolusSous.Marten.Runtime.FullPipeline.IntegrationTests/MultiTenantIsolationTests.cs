using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class MultiTenantIsolationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory factory = factory;

    [Fact(DisplayName = "Multi-tenant - tenants do not see each other's records")]
    public async Task Tenants_do_not_see_each_other_records()
    {
        using var clientA = factory.CreateClientWithTenant("tenant-alpha");
        using var clientB = factory.CreateClientWithTenant("tenant-beta");

        await clientA.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Alpha Item",
            Category = "Main",
            Price = 50
        });

        await clientB.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Beta Item",
            Category = "Main",
            Price = 60
        });

        var itemsA = await clientA.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        var itemsB = await clientB.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");

        itemsA.ShouldNotBeNull();
        itemsB.ShouldNotBeNull();
        itemsA!.ShouldContain(x => x.Name == "Alpha Item");
        itemsA.ShouldNotContain(x => x.Name == "Beta Item");
        itemsB!.ShouldContain(x => x.Name == "Beta Item");
        itemsB.ShouldNotContain(x => x.Name == "Alpha Item");
    }

    [Fact(DisplayName = "Multi-tenant - cannot access resource by id from another tenant")]
    public async Task Tenant_cannot_access_other_tenant_resource_by_id()
    {
        using var clientA = factory.CreateClientWithTenant("tenant-alpha");
        using var clientB = factory.CreateClientWithTenant("tenant-beta");

        var createResponse = await clientA.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Alpha Secret",
            Category = "Main",
            Price = 70
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var response = await clientB.GetAsync($"/api/menu-items/{created!.Id}");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Multi-tenant - delete in tenant A does not affect tenant B list")]
    public async Task Delete_in_one_tenant_does_not_affect_other_tenant()
    {
        using var clientA = factory.CreateClientWithTenant("tenant-alpha");
        using var clientB = factory.CreateClientWithTenant("tenant-beta");

        var createA = await clientA.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Alpha Delete",
            Category = "Main",
            Price = 55
        });
        createA.EnsureSuccessStatusCode();
        var createdA = await createA.Content.ReadFromJsonAsync<MenuItem>();
        createdA.ShouldNotBeNull();

        var createB = await clientB.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Beta Keep",
            Category = "Main",
            Price = 65
        });
        createB.EnsureSuccessStatusCode();
        var createdB = await createB.Content.ReadFromJsonAsync<MenuItem>();
        createdB.ShouldNotBeNull();

        var getB = await clientB.GetAsync($"/api/menu-items/{createdB!.Id}");
        getB.EnsureSuccessStatusCode();

        var deleteResponse = await clientA.DeleteAsync($"/api/menu-items/{createdA!.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var afterDeleteB = await clientB.GetAsync($"/api/menu-items/{createdB.Id}");
        afterDeleteB.EnsureSuccessStatusCode();
    }
}
