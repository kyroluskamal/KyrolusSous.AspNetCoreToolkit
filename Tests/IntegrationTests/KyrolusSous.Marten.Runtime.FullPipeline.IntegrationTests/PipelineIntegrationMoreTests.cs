using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

[Collection("MartenPipelineTestCollection")]
public sealed class CrudPipelineIntegrationMoreTests(TestAppFactory factory)
{
    [Fact(DisplayName = "CRUD pipeline - create returns 201 and item id")]
    public async Task Create_returns_201_and_id()
    {
        using var client = factory.CreateClientWithTenant("tenant-create-201");
        var response = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Create 201",
            Category = "Main",
            Price = 15
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();
        created!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact(DisplayName = "CRUD pipeline - bulk create returns list")]
    public async Task Bulk_create_returns_list()
    {
        using var client = factory.CreateClientWithTenant("tenant-bulk-create");
        var response = await client.PostAsJsonAsync("/api/menu-items", new[]
        {
            new MenuItem { Name = "Bulk 1", Category = "Main", Price = 10 },
            new MenuItem { Name = "Bulk 2", Category = "Main", Price = 12 }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        created.ShouldNotBeNull();
        created!.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "CRUD pipeline - update sets UpdatedAt")]
    public async Task Update_sets_updated_at()
    {
        using var client = factory.CreateClientWithTenant("tenant-update-updatedat");
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Update Time",
            Category = "Main",
            Price = 20
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        created!.Price = 25;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{created.Id}", created);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<MenuItem>();
        updated.ShouldNotBeNull();
        updated!.UpdatedAt.ShouldNotBeNull();
    }

    [Fact(DisplayName = "CRUD pipeline - patch returns success")]
    public async Task Patch_returns_success()
    {
        using var client = factory.CreateClientWithTenant("tenant-patch-price");
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Patch Price",
            Category = "Main",
            Price = 10
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/menu-items/{created!.Id}",
            new Dictionary<string, object> { ["Price"] = 99 });
        patchResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync($"/api/menu-items/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact(DisplayName = "CRUD pipeline - update range is not enabled")]
    public async Task Update_range_is_not_enabled()
    {
        using var client = factory.CreateClientWithTenant("tenant-update-range");
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new[]
        {
            new MenuItem { Name = "Range 1", Category = "Main", Price = 10 },
            new MenuItem { Name = "Range 2", Category = "Main", Price = 12 }
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        created.ShouldNotBeNull();
        created!.ForEach(item => item.Price += 5);

        var updateResponse = await client.PutAsJsonAsync("/api/menu-items", created);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact(DisplayName = "CRUD pipeline - delete range is not enabled")]
    public async Task Delete_range_is_not_enabled()
    {
        using var client = factory.CreateClientWithTenant("tenant-delete-range");
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new[]
        {
            new MenuItem { Name = "Delete 1", Category = "Main", Price = 10 },
            new MenuItem { Name = "Delete 2", Category = "Main", Price = 12 }
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        created.ShouldNotBeNull();
        var ids = created!.Select(x => x.Id).ToArray();

        var deleteResponse = await client.DeleteAsync($"/api/menu-items?ids={string.Join("&ids=", ids)}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact(DisplayName = "CRUD pipeline - includeDeleted false returns list")]
    public async Task Include_deleted_false_returns_list()
    {
        using var client = factory.CreateClientWithTenant("tenant-hide-deleted");
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Hidden Deleted",
            Category = "Main",
            Price = 10
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{created!.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync("/api/menu-items");
        listResponse.EnsureSuccessStatusCode();
        var items = await listResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        var deleted = items!.FirstOrDefault(x => x.Id == created.Id);
        if (deleted is not null)
        {
            deleted.IsDeleted.ShouldBeTrue();
        }
    }

    [Fact(DisplayName = "CRUD pipeline - fields query returns success (list)")]
    public async Task Fields_query_list_returns_success()
    {
        using var client = factory.CreateClientWithTenant("tenant-fields-list");
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Field Item",
            Category = "Main",
            Price = 10
        });

        var response = await client.GetAsync("/api/menu-items?fields=Name");
        response.EnsureSuccessStatusCode();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CRUD pipeline - fields query returns success (single)")]
    public async Task Fields_query_single_returns_success()
    {
        using var client = factory.CreateClientWithTenant("tenant-fields-single");
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Field One",
            Category = "Main",
            Price = 10
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();

        var response = await client.GetAsync($"/api/menu-items/{created!.Id}?fields=Name");
        response.EnsureSuccessStatusCode();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CRUD pipeline - filter by category returns only matching items")]
    public async Task Filter_by_category_returns_matching_items()
    {
        using var client = factory.CreateClientWithTenant("tenant-filter-category");
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem { Name = "Cat A", Category = "Main", Price = 10 });
        await client.PostAsJsonAsync("/api/menu-items", new MenuItem { Name = "Cat B", Category = "Drinks", Price = 5 });

        var filter = Uri.EscapeDataString("Category==\"Main\"");
        var response = await client.GetAsync($"/api/menu-items?filter={filter}");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.ShouldAllBe(x => x.Category == "Main");
    }
}

[Collection("MartenPipelineTestCollection")]
public sealed class AuthPipelineIntegrationMoreTests
{
    private readonly TestAppFactory factory;

    public AuthPipelineIntegrationMoreTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact(DisplayName = "Auth - token response includes access_token")]
    public async Task Token_response_includes_access_token()
    {
        using var client = factory.CreateClientWithTenant("tenant-auth-token");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        token.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Auth - secure endpoint rejects invalid token")]
    public async Task Secure_endpoint_rejects_invalid_token()
    {
        using var client = factory.CreateClientWithTenant("tenant-auth-invalid");
        client.SetBearerToken("invalid-token");
        var response = await client.GetAsync("/api/diagnostics/secure");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Auth - token endpoint rejects invalid grant")]
    public async Task Token_endpoint_rejects_invalid_grant()
    {
        using var client = factory.CreateClientWithTenant("tenant-auth-grant");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "invalid",
            ["client_secret"] = "invalid",
            ["scope"] = "api"
        };
        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

[Collection("MartenPipelineTestCollection")]
public sealed class ProtectedReadPipelineIntegrationMoreTests
{
    private readonly TestAppFactory factory;

    public ProtectedReadPipelineIntegrationMoreTests(TestAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact(DisplayName = "Protected read endpoint - get by id returns order when authorized")]
    public async Task Get_order_by_id_returns_order_when_authorized()
    {
        using var client = factory.CreateClientWithTenant("tenant-orders-get");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var orderResponse = await client.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(
            "customer@local.test",
            "card",
            new List<OrderLine>
            {
                new() { MenuItemId = Guid.NewGuid(), Name = "Pizza", UnitPrice = 10, Quantity = 1 }
            }));
        orderResponse.EnsureSuccessStatusCode();
        var order = await orderResponse.Content.ReadFromJsonAsync<Order>();
        order.ShouldNotBeNull();

        var getResponse = await client.GetAsync($"/api/orders/{order!.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact(DisplayName = "Protected read endpoint - get by id requires authentication")]
    public async Task Get_order_by_id_requires_authentication()
    {
        using var client = factory.CreateClientWithTenant("tenant-orders-auth");
        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

[Collection("MartenPipelineTestCollection")]
public sealed class DataProtectionPipelineIntegrationMoreTests(TestAppFactory factory)
{

    [Fact(DisplayName = "DataProtection - protected value changes with input")]
    public async Task Protected_value_changes_with_input()
    {
        using var client = factory.CreateClientWithTenant("tenant-protect-input");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var responseA = await client.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("value-a"));
        var responseB = await client.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("value-b"));
        responseA.EnsureSuccessStatusCode();
        responseB.EnsureSuccessStatusCode();

        var payloadA = await responseA.Content.ReadFromJsonAsync<ProtectResponse>();
        var payloadB = await responseB.Content.ReadFromJsonAsync<ProtectResponse>();
        payloadA.ShouldNotBeNull();
        payloadB.ShouldNotBeNull();
        payloadA!.Protected.ShouldNotBe(payloadB!.Protected);
    }

    [Fact(DisplayName = "DataProtection - tenant id in response matches header")]
    public async Task Tenant_id_matches_header()
    {
        using var client = factory.CreateClientWithTenant("tenant-protect-echo");
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var response = await client.PostAsJsonAsync("/api/diagnostics/protect", new ProtectRequest("echo"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProtectResponse>();
        payload.ShouldNotBeNull();
        payload!.TenantId.ShouldBe("tenant-protect-echo");
    }
}
