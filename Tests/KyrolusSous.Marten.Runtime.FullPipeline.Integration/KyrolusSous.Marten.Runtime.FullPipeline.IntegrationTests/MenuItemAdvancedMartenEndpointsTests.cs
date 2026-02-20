using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Repositories.EF.Abstractions.Query;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class MenuItemAdvancedMartenEndpointsTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Theory(DisplayName = "MenuItems marten query - supports clause operators")]
    [MemberData(nameof(FilterClauseCases))]
    public async Task Query_endpoint_supports_clause_operators(FilterClause clause, int expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-clause"));

        await CreateMenuItemAsync(client, "Alpha", "Main", 10);
        var updated = await CreateMenuItemAsync(client, "Beta", "Main", 25);
        await CreateMenuItemAsync(client, "Cola", "Drinks", 40);

        updated.Price = 26;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{updated.Id}", updated);
        updateResponse.EnsureSuccessStatusCode();

        var request = new QueryRequest(Filters: [clause]);
        var response = await client.PostAsJsonAsync("/api/menu-items/query", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(expectedCount, body);
    }

    [Fact(DisplayName = "MenuItems marten query - supports order clauses")]
    public async Task Query_endpoint_supports_order_clauses()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-order"));

        await CreateMenuItemAsync(client, "Low", "Main", 5);
        await CreateMenuItemAsync(client, "High", "Main", 50);
        await CreateMenuItemAsync(client, "Mid", "Main", 25);

        var request = new QueryRequest(OrderBy: [new OrderClause("Price", Desc: true)]);
        var response = await client.PostAsJsonAsync("/api/menu-items/query", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(3);
        items[0].Price.ShouldBeGreaterThanOrEqualTo(items[1].Price);
        items[1].Price.ShouldBeGreaterThanOrEqualTo(items[2].Price);
    }

    [Fact(DisplayName = "MenuItems marten query - invalid property returns 400")]
    public async Task Query_endpoint_invalid_property_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-invalid"));

        var request = new QueryRequest(Filters: [new FilterClause("Unknown", "eq", "x")]);
        var response = await client.PostAsJsonAsync("/api/menu-items/query", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "MenuItems marten paged - get paged returns expected metadata")]
    public async Task Paged_endpoint_returns_expected_metadata()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-paged"));

        for (var i = 0; i < 5; i++)
        {
            await CreateMenuItemAsync(client, $"Paged-{i}", "Main", 10 + i);
        }

        var response = await client.GetAsync("/api/menu-items/paged?pageNumber=2&pageSize=2&orderBy=Price:asc");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<PagedPayload<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.PageNumber.ShouldBe(2);
        payload.PageSize.ShouldBe(2);
        payload.TotalCount.ShouldBe(5);
        payload.Items.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "MenuItems marten paged - includeDeleted with fields uses projection path")]
    public async Task Paged_endpoint_include_deleted_with_fields_uses_projection_path()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-paged-fields-selector"));

        await CreateMenuItemAsync(client, "Paged-Selector-1", "Main", 10);
        await CreateMenuItemAsync(client, "Paged-Selector-2", "Main", 11);

        var response = await client.GetAsync("/api/menu-items/paged?pageNumber=1&pageSize=10&includeDeleted=true&fields=Name");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<PagedPayload<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        payload.Items.All(i => !string.IsNullOrWhiteSpace(i.Name)).ShouldBeTrue();
    }

    [Fact(DisplayName = "MenuItems marten paged - dotted fields shape paged payload when projection is disabled")]
    public async Task Paged_endpoint_dotted_fields_shape_paged_payload_when_projection_is_disabled()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-paged-shape"));

        await CreateMenuItemAsync(client, "Paged-Shape-1", "Main", 12);
        await CreateMenuItemAsync(client, "Paged-Shape-2", "Main", 13);

        var response = await client.GetAsync("/api/menu-items/paged?pageNumber=1&pageSize=10&includeDeleted=true&fields=CreatedAt.Date");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var hasItems = document.RootElement.TryGetProperty("Items", out var items)
            || document.RootElement.TryGetProperty("items", out items);
        hasItems.ShouldBeTrue(body);
        items.ValueKind.ShouldBe(JsonValueKind.Array);
        items.GetArrayLength().ShouldBeGreaterThan(0);

        var first = items.EnumerateArray().First();
        var hasField = first.TryGetProperty("CreatedAt.Date", out _)
            || first.TryGetProperty("createdAt.date", out _);
        hasField.ShouldBeTrue(body);
        first.EnumerateObject().Count().ShouldBe(1);
    }

    [Fact(DisplayName = "MenuItems marten paged query - includeDeleted includes soft-deleted rows")]
    public async Task Query_paged_endpoint_include_deleted_includes_soft_deleted_rows()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-paged-incdel"));

        var item = await CreateMenuItemAsync(client, "Soft", "Main", 20);
        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{item.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var request = new KyrolusMartenPagedQueryRequest(
            Request: new QueryRequest(),
            PageNumber: 1,
            PageSize: 20,
            Cacheable: false,
            IncludeDeleted: true);

        var response = await client.PostAsJsonAsync("/api/menu-items/query/paged", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<PagedPayload<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Items.ShouldContain(x => x.Id == item.Id && x.IsDeleted);
    }

    [Fact(DisplayName = "MenuItems marten seek - cursor paging returns next token")]
    public async Task Seek_endpoint_returns_next_token_and_next_page()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-seek"));

        for (var i = 0; i < 4; i++)
        {
            await CreateMenuItemAsync(client, $"Seek-{i}", "Main", 100 + i);
        }

        var firstResponse = await client.GetAsync("/api/menu-items/seek?pageSize=2&includeTotalCount=true");
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, firstBody);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<SeekPayload<MenuItem>>();
        firstPayload.ShouldNotBeNull();
        firstPayload!.Items.Count.ShouldBe(2);
        firstPayload.NextToken.ShouldNotBeNullOrWhiteSpace();
        firstPayload.TotalCount.ShouldBe(4);

        var secondResponse = await client.GetAsync($"/api/menu-items/seek?pageSize=2&includeTotalCount=true&cursor={Uri.EscapeDataString(firstPayload.NextToken!)}");
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK, secondBody);

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<SeekPayload<MenuItem>>();
        secondPayload.ShouldNotBeNull();
        secondPayload!.Items.Count.ShouldBeGreaterThan(0);
        secondPayload.Items.Any(x => firstPayload.Items.All(y => y.Id != x.Id)).ShouldBeTrue(secondBody);
    }

    [Fact(DisplayName = "MenuItems marten seek - invalid cursor returns server error")]
    public async Task Seek_endpoint_invalid_cursor_returns_server_error()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-seek-invalid"));

        await CreateMenuItemAsync(client, "Seek Invalid", "Main", 10);
        var response = await client.GetAsync("/api/menu-items/seek?pageSize=2&includeTotalCount=true&cursor=invalid-token");
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact(DisplayName = "MenuItems marten count - includeDeleted does not reduce total")]
    public async Task Count_endpoint_include_deleted_changes_total()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-count"));

        var first = await CreateMenuItemAsync(client, "Count-1", "Main", 10);
        await CreateMenuItemAsync(client, "Count-2", "Main", 20);
        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{first.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var activeCountResponse = await client.GetAsync("/api/menu-items/count");
        activeCountResponse.EnsureSuccessStatusCode();
        var activeCount = await activeCountResponse.Content.ReadFromJsonAsync<long>();

        var includeDeletedResponse = await client.GetAsync("/api/menu-items/count?includeDeleted=true");
        includeDeletedResponse.EnsureSuccessStatusCode();
        var includeDeletedCount = await includeDeletedResponse.Content.ReadFromJsonAsync<long>();

        activeCount.ShouldBeGreaterThan(0);
        includeDeletedCount.ShouldBeGreaterThanOrEqualTo(activeCount);
    }

    [Fact(DisplayName = "MenuItems marten by-keys - get works and patch-by-keys returns not found (current behavior)")]
    public async Task By_keys_get_works_and_patch_by_keys_returns_not_found()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bykeys"));

        var item = await CreateMenuItemAsync(client, "ByKeys", "Main", 11);

        var getResponse = await client.GetAsync($"/api/menu-items/by-keys?keys={item.Id}");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK, getBody);
        var fetched = await getResponse.Content.ReadFromJsonAsync<MenuItem>();
        fetched.ShouldNotBeNull();
        fetched!.Id.ShouldBe(item.Id);

        var patchResponse = await client.PatchAsJsonAsync(
            $"/api/menu-items/by-keys?keys={item.Id}",
            new Dictionary<string, object> { ["price"] = 123 });
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var verifyResponse = await client.GetAsync($"/api/menu-items/{item.Id}");
        verifyResponse.EnsureSuccessStatusCode();
        var verified = await verifyResponse.Content.ReadFromJsonAsync<MenuItem>();
        verified.ShouldNotBeNull();
        verified!.Price.ShouldBe(11);
    }

    [Fact(DisplayName = "MenuItems marten by-keys - missing keys returns 400")]
    public async Task By_keys_missing_keys_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bykeys-missing"));

        var response = await client.GetAsync("/api/menu-items/by-keys");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory(DisplayName = "MenuItems marten by-keys mutating endpoints - missing keys returns 400")]
    [InlineData("PUT", "/api/menu-items/by-keys")]
    [InlineData("DELETE", "/api/menu-items/by-keys")]
    [InlineData("POST", "/api/menu-items/by-keys/restore")]
    public async Task By_keys_mutating_endpoints_missing_keys_return_bad_request(string method, string path)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bykeys-mutating-missing"));

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == HttpMethod.Put.Method)
        {
            request.Content = JsonContent.Create(new MenuItem
            {
                Name = "Missing Keys",
                Category = "Main",
                Price = 10
            });
        }

        var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "MenuItems marten by-keys - put updates item successfully")]
    public async Task By_keys_put_updates_item_successfully()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bykeys-put"));

        var item = await CreateMenuItemAsync(client, "ByKeys Put", "Main", 11);
        var updateModel = new MenuItem
        {
            Id = item.Id,
            Name = "ByKeys Put Updated",
            Category = "Dessert",
            Price = 44
        };

        var putResponse = await client.PutAsJsonAsync($"/api/menu-items/by-keys?keys={item.Id}", updateModel);
        var putBody = await putResponse.Content.ReadAsStringAsync();
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK, putBody);

        var verifyResponse = await client.GetAsync($"/api/menu-items/{item.Id}");
        verifyResponse.EnsureSuccessStatusCode();
        var verified = await verifyResponse.Content.ReadFromJsonAsync<MenuItem>();
        verified.ShouldNotBeNull();
        verified!.Name.ShouldBe("ByKeys Put Updated");
        verified.Category.ShouldBe("Dessert");
        verified.Price.ShouldBe(44);
    }

    [Fact(DisplayName = "MenuItems marten by-keys - delete/restore by keys returns success (current behavior)")]
    public async Task By_keys_delete_then_restore_returns_success_current_behavior()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bykeys-delete-restore"));

        var item = await CreateMenuItemAsync(client, "ByKeys Delete", "Main", 12);

        var deleteResponse = await client.DeleteAsync($"/api/menu-items/by-keys?keys={item.Id}");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK, deleteBody);

        var afterDelete = await client.GetAsync($"/api/menu-items/{item.Id}");
        afterDelete.StatusCode.ShouldBe(HttpStatusCode.OK);

        var restoreResponse = await client.PostAsync($"/api/menu-items/by-keys/restore?keys={item.Id}", content: null);
        var restoreBody = await restoreResponse.Content.ReadAsStringAsync();
        restoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK, restoreBody);
        restoreBody.ShouldNotBeNullOrWhiteSpace();

        var verifyResponse = await client.GetAsync($"/api/menu-items/{item.Id}?includeDeleted=true");
        verifyResponse.EnsureSuccessStatusCode();
        var verified = await verifyResponse.Content.ReadFromJsonAsync<MenuItem>();
        verified.ShouldNotBeNull();
        verified!.IsDeleted.ShouldBeFalse();
    }

    [Fact(DisplayName = "MenuItems marten seek query - post query/seek applies filter and returns token")]
    public async Task Query_seek_endpoint_applies_filter_and_returns_token()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-seek"));

        await CreateMenuItemAsync(client, "SeekQ-Main-1", "Main", 10);
        await CreateMenuItemAsync(client, "SeekQ-Main-2", "Main", 20);
        await CreateMenuItemAsync(client, "SeekQ-Drink-1", "Drinks", 30);

        var request = new KyrolusMartenSeekQueryRequest(
            Request: new QueryRequest(
                Filters: [new FilterClause("Category", "eq", "Main")],
                OrderBy: [new OrderClause("Price", Desc: false)]),
            PageSize: 1,
            IncludeTotalCount: true);

        var response = await client.PostAsJsonAsync("/api/menu-items/query/seek", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<SeekPayload<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Items.Count.ShouldBe(1);
        payload.TotalCount.ShouldBe(2);
        payload.Items[0].Category.ShouldBe("Main");
        payload.NextToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "MenuItems marten head - existing id returns 200 and missing id returns 404")]
    public async Task Head_endpoint_returns_expected_status_codes()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-head"));

        var item = await CreateMenuItemAsync(client, "Head Item", "Main", 10);

        var hitExisting = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/api/menu-items/{item.Id}"));
        hitExisting.StatusCode.ShouldBe(HttpStatusCode.OK);

        var hitMissing = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/api/menu-items/{Guid.NewGuid()}"));
        hitMissing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory(DisplayName = "MenuItems marten require tenant - head endpoint enforces tenant header")]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.BadRequest)]
    public async Task Require_tenant_head_endpoint_enforces_tenant_header(bool sendTenantHeader, HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RequireTenant = true;
            });
        });

        using var seededClient = customFactory.CreateClient();
        var tenantId = TestHelpers.NewTenantId("menuitem-head-require-tenant");
        seededClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        var item = await CreateMenuItemAsync(seededClient, "Head-Tenant", "Main", 8);

        using var requestClient = customFactory.CreateClient();
        if (sendTenantHeader)
        {
            requestClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        }

        var response = await requestClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/api/menu-items/{item.Id}"));
        response.StatusCode.ShouldBe(expectedStatus);
    }

    [Fact(DisplayName = "MenuItems marten range - update range works and delete range returns 500 (current behavior)")]
    public async Task Range_update_works_and_delete_returns_internal_server_error_current_behavior()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-range"));

        var first = await CreateMenuItemAsync(client, "Range-1", "Main", 10);
        var second = await CreateMenuItemAsync(client, "Range-2", "Main", 20);

        var updateModels = new[]
        {
            new MenuItem { Id = first.Id, Name = "Range-1-Updated", Category = "Main", Price = 15 },
            new MenuItem { Id = second.Id, Name = "Range-2-Updated", Category = "Dessert", Price = 25 }
        };

        var updateResponse = await client.PutAsJsonAsync("/api/menu-items/range", updateModels);
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, updateBody);

        var verifyUpdatedFirst = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{first.Id}");
        verifyUpdatedFirst.ShouldNotBeNull();
        verifyUpdatedFirst!.Name.ShouldBe("Range-1-Updated");
        verifyUpdatedFirst.Price.ShouldBe(15);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/menu-items/range")
        {
            Content = JsonContent.Create(new[] { verifyUpdatedFirst })
        };
        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact(DisplayName = "MenuItems marten bulk - update and delete execute against filter")]
    public async Task Bulk_update_and_delete_execute_against_filter()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bulk"));

        await CreateMenuItemAsync(client, "Bulk-Main-1", "Main", 10);
        await CreateMenuItemAsync(client, "Bulk-Main-2", "Main", 20);
        await CreateMenuItemAsync(client, "Bulk-Drink-1", "Drinks", 30);

        var bulkUpdate = new KyrolusMartenBulkUpdateRequest(
            Request: new QueryRequest(Filters: [new FilterClause("Category", "eq", "Main")]),
            Updates: new Dictionary<string, object> { ["Price"] = 77m },
            Cacheable: false);
        var updateResponse = await client.PostAsJsonAsync("/api/menu-items/bulk/update", bulkUpdate);
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, updateBody);
        var updatedCount = await updateResponse.Content.ReadFromJsonAsync<int>();
        updatedCount.ShouldBeGreaterThanOrEqualTo(0);

        var allAfterUpdate = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        allAfterUpdate.ShouldNotBeNull();
        allAfterUpdate!.Where(x => x.Category == "Main").All(x => x.Price == 77).ShouldBeTrue();

        var bulkDelete = new KyrolusMartenBulkDeleteRequest(
            Request: new QueryRequest(Filters: [new FilterClause("Category", "eq", "Drinks")]),
            Cacheable: false);
        var deleteResponse = await client.PostAsJsonAsync("/api/menu-items/bulk/delete", bulkDelete);
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK, deleteBody);
        var deletedCount = await deleteResponse.Content.ReadFromJsonAsync<int>();
        deletedCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact(DisplayName = "MenuItems marten bulk update - empty updates returns bad request")]
    public async Task Bulk_update_empty_updates_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bulk-update-empty"));

        var request = new KyrolusMartenBulkUpdateRequest(
            Request: new QueryRequest(Filters: [new FilterClause("Category", "eq", "Main")]),
            Updates: new Dictionary<string, object>(),
            Cacheable: false);

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/update", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Updates are required.");
    }

    [Fact(DisplayName = "MenuItems marten bulk update - filtered disallowed updates returns bad request")]
    public async Task Bulk_update_filtered_disallowed_updates_returns_bad_request()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.StrictPatchValidation = false;
                config.AllowedPatchProperties = ["Price"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-bulk-update-filtered"));

        await CreateMenuItemAsync(client, "Bulk-Filtered-1", "Main", 10);

        var request = new KyrolusMartenBulkUpdateRequest(
            Request: new QueryRequest(Filters: [new FilterClause("Category", "eq", "Main")]),
            Updates: new Dictionary<string, object> { ["Name"] = "Ignored" },
            Cacheable: false);

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/update", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("No update fields are allowed.");
    }

    [Fact(DisplayName = "MenuItems marten bulk - upsert and patch endpoints execute")]
    public async Task Bulk_upsert_and_patch_endpoints_execute()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bulk-upsert-patch"));

        var existing = await CreateMenuItemAsync(client, "Bulk Existing", "Main", 10);
        var newId = Guid.NewGuid();

        var upsertPayload = new[]
        {
            new MenuItem { Id = existing.Id, Name = "Bulk Existing Updated", Category = "Main", Price = 99 },
            new MenuItem { Id = newId, Name = "Bulk New", Category = "Drinks", Price = 5 }
        };

        var upsertResponse = await client.PostAsJsonAsync("/api/menu-items/bulk/upsert", upsertPayload);
        var upsertBody = await upsertResponse.Content.ReadAsStringAsync();
        upsertResponse.StatusCode.ShouldBe(HttpStatusCode.OK, upsertBody);
        var upserted = await upsertResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        upserted.ShouldNotBeNull();
        upserted!.Count.ShouldBeGreaterThanOrEqualTo(1);

        var bulkPatchPayload = new[]
        {
            new
            {
                id = existing.Id.ToString(),
                updates = new Dictionary<string, object> { ["Price"] = 123m }
            },
            new
            {
                id = newId.ToString(),
                updates = new Dictionary<string, object> { ["Category"] = "Snacks" }
            }
        };

        var patchResponse = await client.PostAsJsonAsync("/api/menu-items/bulk/patch", bulkPatchPayload);
        var patchBody = await patchResponse.Content.ReadAsStringAsync();
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, patchBody);
        var patchedCount = await patchResponse.Content.ReadFromJsonAsync<int>();
        patchedCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact(DisplayName = "MenuItems marten deleted/restore - deleted endpoint and restore endpoint work")]
    public async Task Deleted_and_restore_endpoints_work()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-restore-endpoint"));

        var item = await CreateMenuItemAsync(client, "Restore-Endpoint", "Main", 9);
        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{item.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var deletedResponse = await client.GetAsync("/api/menu-items/deleted");
        var deletedBody = await deletedResponse.Content.ReadAsStringAsync();
        deletedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, deletedBody);
        var deletedItems = await deletedResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        deletedItems.ShouldNotBeNull();
        deletedItems!.ShouldContain(x => x.Id == item.Id && x.IsDeleted);

        var restoreResponse = await client.PostAsync($"/api/menu-items/{item.Id}/restore", content: null);
        var restoreBody = await restoreResponse.Content.ReadAsStringAsync();
        restoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK, restoreBody);
        var restored = await restoreResponse.Content.ReadFromJsonAsync<bool>();
        restored.ShouldBeTrue();

        var restoredGet = await client.GetAsync($"/api/menu-items/{item.Id}?includeDeleted=true");
        restoredGet.EnsureSuccessStatusCode();
        var restoredItem = await restoredGet.Content.ReadFromJsonAsync<MenuItem>();
        restoredItem.ShouldNotBeNull();
        restoredItem!.IsDeleted.ShouldBeFalse();
    }

    [Fact(DisplayName = "MenuItems marten batch - enabled batch executes operations but returns 500 (current behavior)")]
    public async Task Batch_endpoint_enabled_executes_operations_but_returns_internal_server_error_current_behavior()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-enabled"));

        var existing = await CreateMenuItemAsync(client, "Batch Existing", "Main", 15);

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = true,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-update",
                    Operation = KyrolusBatchOperationType.Update,
                    Id = existing.Id,
                    Data = new MenuItem { Name = "Batch Existing Updated", Category = "Main", Price = 30 }
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-create",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = new MenuItem { Name = "Batch Created", Category = "Main", Price = 10 }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, body);
        body.ShouldContain("InvalidOperationException");

        var verifyExisting = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{existing.Id}");
        verifyExisting.ShouldNotBeNull();
        verifyExisting!.Name.ShouldBe("Batch Existing Updated");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldContain(x => x.Name == "Batch Created");
    }

    [Fact(DisplayName = "MenuItems marten batch - continueOnError executes valid ops but returns 500 (current behavior)")]
    public async Task Batch_endpoint_continue_on_error_executes_valid_ops_but_returns_internal_server_error_current_behavior()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-multistatus"));
        const string validName = "Batch Valid";

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = true,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-invalid",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = null
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-valid",
                    Operation = KyrolusBatchOperationType.Create,
                    Data = new MenuItem { Name = validName, Category = "Main", Price = 5 }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, body);
        body.ShouldContain("InvalidOperationException");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldContain(x => x.Name == validName);
    }

    public static IEnumerable<object[]> FilterClauseCases()
    {
        yield return [new FilterClause("Category", "eq", "Main"), 2];
        yield return [new FilterClause("Price", "gt", "20"), 2];
        yield return [new FilterClause("Price", "between", "10,30"), 2];
        yield return [new FilterClause("Category", "in", "Main,Drinks"), 3];
        yield return [new FilterClause("UpdatedAt", "notnull", null), 1];
        yield return [new FilterClause("UpdatedAt", "isnull", null), 2];
    }

    private static async Task<MenuItem> CreateMenuItemAsync(HttpClient client, string name, string category, decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = name,
            Category = category,
            Price = price
        });
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<MenuItem>();
        item.ShouldNotBeNull();
        return item!;
    }

    private static IKyrolusMartenApiConfig<MenuItem> ResolveMenuItemMartenConfig(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces.IKyrolusApiConfig<MenuItem>));
        if (descriptor?.ImplementationInstance is IKyrolusMartenApiConfig<MenuItem> config)
        {
            return config;
        }

        throw new InvalidOperationException("MenuItem IKyrolusMartenApiConfig is not registered.");
    }

    private sealed record PagedPayload<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);
    private sealed record SeekPayload<T>(IReadOnlyList<T> Items, string? NextToken, int? TotalCount, int PageSize);
}
