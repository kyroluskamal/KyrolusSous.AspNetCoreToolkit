using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

[Collection("MartenPipelineTestCollection")]
public sealed class CrudEndpointContractIntegrationTests
{
    private readonly TestAppFactory factory;

    public CrudEndpointContractIntegrationTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact(DisplayName = "CRUD endpoints - create invalid returns 400")]
    public async Task Create_invalid_payload_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var item = new MenuItem
        {
            Name = "",
            Category = "Drinks",
            Price = 0
        };

        var response = await client.PostAsJsonAsync("/api/menu-items", item);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
    }

    [Fact(DisplayName = "CRUD endpoints - create/update/delete/restore flow")]
    public async Task Create_update_delete_restore_flow()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var item = new MenuItem
        {
            Name = "Burger",
            Category = "Main",
            Price = 120
        };

        var createResponse = await client.PostAsJsonAsync("/api/menu-items", item);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.IsSuccessStatusCode.ShouldBeTrue(createBody);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();
        created!.TenantId.ShouldBe("tenant-alpha", created.TenantId);

        var getResponse = await client.GetAsync($"/api/menu-items/{created.Id}");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.IsSuccessStatusCode.ShouldBeTrue(getBody);

        created.Price = 140;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{created.Id}", created);
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.IsSuccessStatusCode.ShouldBeTrue(updateBody);

        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{created.Id}");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.IsSuccessStatusCode.ShouldBeTrue(deleteBody);

        var deletedGet = await client.GetAsync($"/api/menu-items/{created.Id}?includeDeleted=true");
        var deletedGetBody = await deletedGet.Content.ReadAsStringAsync();
        deletedGet.IsSuccessStatusCode.ShouldBeTrue(deletedGetBody);
        var deletedItem = await deletedGet.Content.ReadFromJsonAsync<MenuItem>();
        deletedItem.ShouldNotBeNull();
        deletedItem!.IsDeleted.ShouldBeTrue();

        var deletedResponse = await client.GetAsync("/api/menu-items?includeDeleted=true");
        var deletedBody = await deletedResponse.Content.ReadAsStringAsync();
        deletedResponse.IsSuccessStatusCode.ShouldBeTrue(deletedBody);
        var deletedItems = await deletedResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        deletedItems.ShouldNotBeNull();
        var deletedIds = string.Join(", ", deletedItems!.Select(x => x.Id));
        deletedItems.ShouldContain(x => x.Id == created.Id, deletedIds);

        var restoreResponse = await client.PatchAsJsonAsync($"/api/menu-items/{created.Id}",
            new Dictionary<string, object> { ["isDeleted"] = false });
        var restoreBody = await restoreResponse.Content.ReadAsStringAsync();
        restoreResponse.IsSuccessStatusCode.ShouldBeTrue(restoreBody);

        var restoredGet = await client.GetAsync($"/api/menu-items/{created.Id}");
        var restoredBody = await restoredGet.Content.ReadAsStringAsync();
        restoredGet.IsSuccessStatusCode.ShouldBeTrue(restoredBody);
    }

    [Fact(DisplayName = "CRUD endpoints - query endpoint returns items")]
    public async Task Query_endpoint_returns_items()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Cola",
            Category = "Drinks",
            Price = 10
        });

        var listResponse = await client.GetAsync("/api/menu-items");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.IsSuccessStatusCode.ShouldBeTrue(listBody);
        var items = await listResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "CRUD endpoints - get missing item returns 404")]
    public async Task Get_missing_resource_returns_not_found()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var response = await client.GetAsync($"/api/menu-items/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "CRUD endpoints - patch with empty body returns 400")]
    public async Task Patch_with_empty_body_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Patch Item",
            Category = "Main",
            Price = 70
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var patchResponse = await client.PatchAsJsonAsync($"/api/menu-items/{created!.Id}", new Dictionary<string, object>());
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CRUD endpoints - patch cannot update tenant id")]
    public async Task Patch_cannot_update_tenant_id()
    {
        using var client = factory.CreateClientWithTenant("tenant-alpha");
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Tenant Locked",
            Category = "Main",
            Price = 40
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/menu-items/{created!.Id}",
            new Dictionary<string, object> { ["tenantId"] = "tenant-beta" });
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CRUD endpoints - get all returns empty list for new tenant")]
    public async Task Get_all_returns_empty_for_new_tenant()
    {
        using var client = factory.CreateClientWithTenant("tenant-empty-list");
        var response = await client.GetAsync("/api/menu-items");
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(body);
        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "CRUD endpoints - filter by name returns matching items")]
    public async Task Filter_by_name_returns_matching_items()
    {
        using var client = factory.CreateClientWithTenant("tenant-filter");
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Filtered A",
            Category = "Main",
            Price = 25
        });
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Filtered B",
            Category = "Main",
            Price = 35
        });

        var filter = Uri.EscapeDataString("Name==\"Filtered A\"");
        var response = await client.GetAsync($"/api/menu-items?filter={filter}");
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(body);
        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.ShouldContain(x => x.Name == "Filtered A");
    }

    [Fact(DisplayName = "CRUD endpoints - invalid filter returns 400")]
    public async Task Invalid_filter_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant("tenant-filter-invalid");
        var filter = Uri.EscapeDataString("Unknown==1");
        var response = await client.GetAsync($"/api/menu-items?filter={filter}");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CRUD endpoints - create with empty category returns 400")]
    public async Task Create_with_empty_category_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-empty-category"));
        var item = new MenuItem
        {
            Name = "No Category",
            Category = "",
            Price = 10
        };

        var response = await client.PostAsJsonAsync("/api/menu-items", item);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CRUD endpoints - update with negative price returns 400")]
    public async Task Update_with_negative_price_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-negative-price"));
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Negative Price",
            Category = "Main",
            Price = 10
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        created!.Price = -5;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{created.Id}", created);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CRUD endpoints - restore clears IsDeleted flag")]
    public async Task Restore_clears_is_deleted_flag()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-restore"));
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Restore Flag",
            Category = "Main",
            Price = 20
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{created!.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var restoreResponse = await client.PatchAsJsonAsync(
            $"/api/menu-items/{created.Id}",
            new Dictionary<string, object> { ["isDeleted"] = false });
        restoreResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync($"/api/menu-items/{created.Id}?includeDeleted=true");
        getResponse.EnsureSuccessStatusCode();
        var restored = await getResponse.Content.ReadFromJsonAsync<MenuItem>();
        restored.ShouldNotBeNull();
        restored!.IsDeleted.ShouldBeFalse();
    }
}

