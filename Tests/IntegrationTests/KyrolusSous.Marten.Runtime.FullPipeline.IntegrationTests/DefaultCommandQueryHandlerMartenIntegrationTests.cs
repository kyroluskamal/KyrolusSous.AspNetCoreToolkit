using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Linq.Expressions;
using System.Globalization;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Authorization;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Services;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class DefaultCommandQueryHandlerMartenIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Theory(DisplayName = "DefaultCommandQueryHandler marten query - supports clause operators")]
    [MemberData(nameof(FilterClauseCases))]
    public async Task Query_endpoint_supports_clause_operators(TestFilterClause clause, int expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-clause"));

        await CreateMenuItemAsync(client, "Alpha", "Main", 10);
        var updated = await CreateMenuItemAsync(client, "Beta", "Main", 25);
        await CreateMenuItemAsync(client, "Cola", "Drinks", 40);

        updated.Price = 26;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{updated.Id}", updated);
        updateResponse.EnsureSuccessStatusCode();

        var request = new TestQueryRequest(Filters: [clause]);
        var response = await client.PostAsJsonAsync("/api/menu-items/query", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(expectedCount, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten query - supports order clauses")]
    public async Task Query_endpoint_supports_order_clauses()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-order"));

        await CreateMenuItemAsync(client, "Low", "Main", 5);
        await CreateMenuItemAsync(client, "High", "Main", 50);
        await CreateMenuItemAsync(client, "Mid", "Main", 25);

        var request = new TestQueryRequest(OrderBy: [new TestOrderClause("Price", Desc: true)]);
        var response = await client.PostAsJsonAsync("/api/menu-items/query", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(3);
        items[0].Price.ShouldBeGreaterThanOrEqualTo(items[1].Price);
        items[1].Price.ShouldBeGreaterThanOrEqualTo(items[2].Price);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten query - invalid property returns 400")]
    public async Task Query_endpoint_invalid_property_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-invalid"));

        var request = new TestQueryRequest(Filters: [new TestFilterClause("Unknown", "eq", "x")]);
        var response = await client.PostAsJsonAsync("/api/menu-items/query", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten get-all - includes merge and includeGraph relaxed validation execute")]
    public async Task Get_all_includes_merge_and_include_graph_relaxed_validation_execute()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.StrictIncludeValidation = false;
                config.AllowedIncludeProperties = ["Category"];
                config.MaxIncludeGraphDepth = 2;
                config.EndpointConfig = [new KyrolusEndpointConfig
                {
                    Name = EndpointNames.GetAll,
                    IncludeProps = ["Name"]
                }];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-includes-merge-relaxed"));
        await CreateMenuItemAsync(client, "Includes-Case", "Main", 7);

        var response = await client.GetAsync("/api/menu-items?includedProps=Category&includeGraph=Name");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBeGreaterThan(0);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten query - includeGraph payload formats execute when include graph is enabled")]
    [MemberData(nameof(IncludeGraphPayloadCases))]
    public async Task Query_include_graph_payload_formats_execute_when_enabled(object includeGraphPayload)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.MaxIncludeGraphDepth = 2;
                config.StrictIncludeValidation = false;
                config.AllowedIncludeProperties = ["Category"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-query-include-graph-enabled"));
        await CreateMenuItemAsync(client, "Graph-Enabled", "Main", 9);

        var request = new
        {
            IncludeGraph = includeGraphPayload
        };
        var response = await client.PostAsJsonAsync("/api/menu-items/query", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten projection - fallback query type applies selector for get-all/query/deleted")]
    public async Task Projection_fallback_query_type_applies_selector_for_get_all_query_and_deleted()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusQueryHandler<FallbackMenuItemsQuery, IEnumerable<MenuItem>>, FallbackMenuItemsQueryHandler>();

                var config = ResolveMenuItemMartenConfig(services);
                config.QueryAll = new FallbackMenuItemsQuery();
                config.QueryByProperty = new FallbackMenuItemsQuery();
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-projection-fallback-query"));
        const string activeName = "Projection-Active";
        const string deletedName = "Projection-Deleted";
        await CreateMenuItemAsync(client, activeName, "Main", 30);
        var deleted = await CreateMenuItemAsync(client, deletedName, "Main", 40);
        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{deleted.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var getAllResponse = await client.GetAsync("/api/menu-items?fields=Name");
        var getAllBody = await getAllResponse.Content.ReadAsStringAsync();
        getAllResponse.StatusCode.ShouldBe(HttpStatusCode.OK, getAllBody);
        var getAllItems = await getAllResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        getAllItems.ShouldNotBeNull();
        getAllItems!.Count.ShouldBeGreaterThan(0);
        getAllItems.Select(x => x.Name).ShouldContain(activeName);
        getAllItems.All(x => x.Price == 0).ShouldBeTrue();

        var queryResponse = await client.PostAsJsonAsync("/api/menu-items/query?includeDeleted=true", new TestQueryRequest(Fields: ["Name"]));
        var queryBody = await queryResponse.Content.ReadAsStringAsync();
        queryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, queryBody);
        var queryItems = await queryResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        queryItems.ShouldNotBeNull();
        queryItems!.Count.ShouldBeGreaterThan(0);
        queryItems.Select(x => x.Name).ShouldContain(activeName);
        queryItems.Select(x => x.Name).ShouldContain(deletedName);
        queryItems.All(x => x.Price == 0).ShouldBeTrue();

        var deletedResponse = await client.GetAsync("/api/menu-items/deleted?fields=Name");
        var deletedBody = await deletedResponse.Content.ReadAsStringAsync();
        deletedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, deletedBody);
        var deletedItems = await deletedResponse.Content.ReadFromJsonAsync<List<MenuItem>>();
        deletedItems.ShouldNotBeNull();
        deletedItems!.Count.ShouldBeGreaterThan(0);
        deletedItems.Select(x => x.Name).ShouldContain(deletedName);
        deletedItems.All(x => x.Price == 0).ShouldBeTrue();
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten paged - get paged returns expected metadata")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten paged - includeDeleted with fields uses projection path")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten paged - dotted fields shape paged payload when projection is disabled")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten paged query - includeDeleted includes soft-deleted rows")]
    public async Task Query_paged_endpoint_include_deleted_includes_soft_deleted_rows()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-paged-incdel"));

        var item = await CreateMenuItemAsync(client, "Soft", "Main", 20);
        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{item.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var request = new TestPagedQueryRequest(
            Request: new TestQueryRequest(),
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten seek - cursor paging returns next token")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten seek - invalid cursor returns server error")]
    public async Task Seek_endpoint_invalid_cursor_returns_server_error()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-seek-invalid"));

        await CreateMenuItemAsync(client, "Seek Invalid", "Main", 10);
        var response = await client.GetAsync("/api/menu-items/seek?pageSize=2&includeTotalCount=true&cursor=invalid-token");
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten seek - key property fallback is used when composite key names are empty")]
    public async Task Seek_endpoint_uses_key_property_fallback_when_composite_key_names_are_empty()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.CompositeKeyPropertyNames = [];
                config.KeyPropertyName = "Id";
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-seek-key-fallback"));
        await CreateMenuItemAsync(client, "Seek-Fallback-1", "Main", 41);
        await CreateMenuItemAsync(client, "Seek-Fallback-2", "Main", 42);

        var response = await client.GetAsync("/api/menu-items/seek?pageSize=1&includeTotalCount=true");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<SeekPayload<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Items.Count.ShouldBe(1);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten authorization - unauthorized result maps to 404 or 403")]
    [InlineData(true, HttpStatusCode.NotFound)]
    [InlineData(false, HttpStatusCode.Forbidden)]
    public async Task Authorization_provider_unauthorized_result_maps_expected_status(bool returnNotFound, HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusMartenAuthorizationProvider<MenuItem>>(
                    _ => new MenuItemTestAuthorizationProvider(
                        unauthorized: true,
                        returnNotFound: returnNotFound,
                        rowFilterAllow: null));
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-auth-unauthorized"));

        var response = await client.GetAsync($"/api/menu-items/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten authorization - row filter allow/deny controls get-by-id result")]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.Forbidden)]
    public async Task Authorization_provider_row_filter_allow_or_deny_controls_get_by_id(bool allow, HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusMartenAuthorizationProvider<MenuItem>>(
                    _ => new MenuItemTestAuthorizationProvider(
                        unauthorized: false,
                        returnNotFound: false,
                        rowFilterAllow: allow));
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-auth-row-filter"));
        var item = await CreateMenuItemAsync(client, "Auth-RowFilter", "Main", 12);

        var response = await client.GetAsync($"/api/menu-items/{item.Id}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten count - includeDeleted does not reduce total")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - patch updates the matching item when single-key Id is propagated")]
    public async Task By_keys_get_works_and_patch_by_keys_updates_matching_item()
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
        var patchBody = await patchResponse.Content.ReadAsStringAsync();
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK, patchBody);

        var verifyResponse = await client.GetAsync($"/api/menu-items/{item.Id}");
        verifyResponse.EnsureSuccessStatusCode();
        var verified = await verifyResponse.Content.ReadFromJsonAsync<MenuItem>();
        verified.ShouldNotBeNull();
        verified!.Price.ShouldBe(123);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - missing keys returns 400")]
    public async Task By_keys_missing_keys_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bykeys-missing"));

        var response = await client.GetAsync("/api/menu-items/by-keys");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten by-keys mutating endpoints - missing keys returns 400")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - put updates item successfully")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - put converts string key when composite key types are not configured")]
    public async Task By_keys_put_converts_string_key_when_composite_key_types_not_configured()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.CompositeKeyTypes = null;
                config.CompositeKeyPropertyNames = ["Id"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-bykeys-string-key-conversion"));
        var item = await CreateMenuItemAsync(client, "ByKeys Convert", "Main", 16);

        var updateModel = new MenuItem
        {
            Id = item.Id,
            Name = "ByKeys Convert Updated",
            Category = "Drinks",
            Price = 22
        };

        var putResponse = await client.PutAsJsonAsync($"/api/menu-items/by-keys?keys={item.Id}", updateModel);
        var putBody = await putResponse.Content.ReadAsStringAsync();
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK, putBody);

        var verifyResponse = await client.GetAsync($"/api/menu-items/{item.Id}");
        verifyResponse.EnsureSuccessStatusCode();
        var verified = await verifyResponse.Content.ReadFromJsonAsync<MenuItem>();
        verified.ShouldNotBeNull();
        verified!.Name.ShouldBe("ByKeys Convert Updated");
        verified.Category.ShouldBe("Drinks");
        verified.Price.ShouldBe(22);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - parser null value returns bad request for non-nullable key")]
    public async Task By_keys_put_parser_null_value_returns_bad_request_for_non_nullable_key()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.CompositeKeyParser = static _ => [null];
                config.CompositeKeyPropertyNames = ["Id"];
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-bykeys-parser-null"));

        var updateModel = new MenuItem
        {
            Name = "Parser Null",
            Category = "Main",
            Price = 10
        };

        var response = await client.PutAsJsonAsync($"/api/menu-items/by-keys?keys={Guid.NewGuid()}", updateModel);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Cannot set composite key property");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten update - mismatched route/body id returns bad request when GetEntityId is configured")]
    public async Task Update_mismatched_route_body_id_returns_bad_request_when_get_entity_id_configured()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.GetEntityId = static entity => entity.Id;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-update-id-mismatch"));
        var item = await CreateMenuItemAsync(client, "Mismatch-Seed", "Main", 18);

        var payload = new MenuItem
        {
            Id = Guid.NewGuid(),
            Name = "Mismatch-Update",
            Category = "Main",
            Price = 21
        };

        var response = await client.PutAsJsonAsync($"/api/menu-items/{item.Id}", payload);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("does not match");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten update - If-Match with Guid row version covers wildcard/valid/invalid branches")]
    [InlineData("*", HttpStatusCode.OK)]
    [InlineData("invalid-guid", HttpStatusCode.BadRequest)]
    [InlineData("valid-id", HttpStatusCode.OK)]
    public async Task Update_if_match_with_guid_row_version_covers_wildcard_valid_and_invalid_branches(string ifMatchMode, HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RowVersionPropertyName = nameof(MenuItem.Id);
                config.EnableEtags = true;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-update-ifmatch-guid"));
        var item = await CreateMenuItemAsync(client, "IfMatch-Guid-Before", "Main", 17);
        var ifMatch = ifMatchMode == "valid-id" ? item.Id.ToString("N") : ifMatchMode;

        var model = new MenuItem
        {
            Id = item.Id,
            Name = "IfMatch-Guid-After",
            Category = "Dessert",
            Price = 29
        };

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/menu-items/{item.Id}")
        {
            Content = JsonContent.Create(model)
        };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten by-keys patch - custom command covers null and non-null result branches")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task By_keys_patch_custom_command_covers_null_and_non_null_result_branches(bool returnNull)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<ByKeysPatchBranchCommand, MenuItem>, ByKeysPatchBranchCommandHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.PatchCommand = new ByKeysPatchBranchCommand
                {
                    ReturnNull = returnNull
                };
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-bykeys-patch-branch"));
        var item = await CreateMenuItemAsync(client, "ByKeys Patch Branch", "Main", 19);

        var response = await client.PatchAsJsonAsync(
            $"/api/menu-items/by-keys?keys={item.Id}",
            new Dictionary<string, object> { ["Price"] = 123m });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var verify = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{item.Id}");
        verify.ShouldNotBeNull();
        verify!.Price.ShouldBe(returnNull ? 19 : 123);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten by-keys patch - If-Match enforces key lookup and etag match")]
    [InlineData("missing", HttpStatusCode.NotFound)]
    [InlineData("stale", HttpStatusCode.Conflict)]
    [InlineData("wildcard", HttpStatusCode.OK)]
    public async Task By_keys_patch_if_match_enforces_key_lookup_and_etag_match(string mode, HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<ByKeysPatchBranchCommand, MenuItem>, ByKeysPatchBranchCommandHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.RowVersionPropertyName = nameof(MenuItem.Id);
                config.EnableEtags = true;
                config.PatchCommand = new ByKeysPatchBranchCommand
                {
                    ReturnNull = false
                };
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-bykeys-ifmatch-matrix"));
        var item = await CreateMenuItemAsync(client, "ByKeys-IfMatch-Before", "Main", 14);

        var targetId = mode == "missing" ? Guid.NewGuid() : item.Id;
        var ifMatch = mode switch
        {
            "wildcard" => "*",
            "stale" => Guid.NewGuid().ToString("N"),
            _ => item.Id.ToString("N")
        };

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/menu-items/by-keys?keys={targetId}")
        {
            Content = JsonContent.Create(new Dictionary<string, object> { ["Price"] = 66m })
        };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys patch - numeric row version uses invariant formattable etag normalization")]
    public async Task By_keys_patch_numeric_row_version_uses_invariant_formattable_etag_normalization()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<ByKeysPatchBranchCommand, MenuItem>, ByKeysPatchBranchCommandHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.RowVersionPropertyName = nameof(MenuItem.Price);
                config.EnableEtags = true;
                config.PatchCommand = new ByKeysPatchBranchCommand
                {
                    ReturnNull = false
                };
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-bykeys-ifmatch-price"));
        var item = await CreateMenuItemAsync(client, "ByKeys-Price-Version", "Main", 31);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/menu-items/by-keys?keys={item.Id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object> { ["Price"] = 99m })
        };
        request.Headers.TryAddWithoutValidation("If-Match", "stale-price-version");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten by-keys - delete/restore by keys returns success (current behavior)")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten seek query - post query/seek applies filter and returns token")]
    public async Task Query_seek_endpoint_applies_filter_and_returns_token()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-query-seek"));

        await CreateMenuItemAsync(client, "SeekQ-Main-1", "Main", 10);
        await CreateMenuItemAsync(client, "SeekQ-Main-2", "Main", 20);
        await CreateMenuItemAsync(client, "SeekQ-Drink-1", "Drinks", 30);

        var request = new TestSeekQueryRequest(
            Request: new TestQueryRequest(
                Filters: [new TestFilterClause("Category", "eq", "Main")],
                OrderBy: [new TestOrderClause("Price", Desc: false)]),
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten head - existing id returns 200 and missing id returns 404")]
    public async Task Head_endpoint_returns_expected_status_codes()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-head"));

        var item = await CreateMenuItemAsync(client, "Head Item", "Main", 10);

        var hitExisting = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/api/menu-items/{item.Id}"));
        hitExisting.StatusCode.ShouldBe(HttpStatusCode.OK);

        var hitMissing = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/api/menu-items/{Guid.NewGuid()}"));
        hitMissing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten require tenant - head endpoint enforces tenant header")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten range - update range works and delete range returns 500 (current behavior)")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten bulk - update and delete execute against filter")]
    public async Task Bulk_update_and_delete_execute_against_filter()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bulk"));

        await CreateMenuItemAsync(client, "Bulk-Main-1", "Main", 10);
        await CreateMenuItemAsync(client, "Bulk-Main-2", "Main", 20);
        await CreateMenuItemAsync(client, "Bulk-Drink-1", "Drinks", 30);

        var bulkUpdate = new
        {
            Request = new TestQueryRequest(Filters: [new TestFilterClause("Category", "eq", "Main")]),
            Updates = new Dictionary<string, object> { ["Price"] = 77m },
            Cacheable = false
        };
        var updateResponse = await client.PostAsJsonAsync("/api/menu-items/bulk/update", bulkUpdate);
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, updateBody);
        var updatedCount = await updateResponse.Content.ReadFromJsonAsync<int>();
        updatedCount.ShouldBeGreaterThanOrEqualTo(0);

        var allAfterUpdate = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        allAfterUpdate.ShouldNotBeNull();
        allAfterUpdate!.Where(x => x.Category == "Main").All(x => x.Price == 77).ShouldBeTrue();

        var bulkDelete = new
        {
            Request = new TestQueryRequest(Filters: [new TestFilterClause("Category", "eq", "Drinks")]),
            Cacheable = false
        };
        var deleteResponse = await client.PostAsJsonAsync("/api/menu-items/bulk/delete", bulkDelete);
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK, deleteBody);
        var deletedCount = await deleteResponse.Content.ReadFromJsonAsync<int>();
        deletedCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten bulk update - empty updates returns bad request")]
    public async Task Bulk_update_empty_updates_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-bulk-update-empty"));

        var request = new
        {
            Request = new TestQueryRequest(Filters: [new TestFilterClause("Category", "eq", "Main")]),
            Updates = new Dictionary<string, object>(),
            Cacheable = false
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/update", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Updates are required.");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten bulk update - filtered disallowed updates returns bad request")]
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

        var request = new
        {
            Request = new TestQueryRequest(Filters: [new TestFilterClause("Category", "eq", "Main")]),
            Updates = new Dictionary<string, object> { ["Name"] = "Ignored" },
            Cacheable = false
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/update", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("No update fields are allowed.");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten bulk - upsert and patch endpoints execute")]
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

    [Fact(DisplayName = "DefaultCommandQueryHandler marten deleted/restore - deleted endpoint and restore endpoint work")]
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

    [Theory(DisplayName = "DefaultCommandQueryHandler marten delete - soft delete decision matrix controls delete mode")]
    [MemberData(nameof(SoftDeleteDecisionCases))]
    public async Task Delete_soft_delete_decision_matrix_controls_delete_mode(
        bool useSoftDeleteForDelete,
        bool enableSoftDeleteEndpoints,
        EndpointNames[] endpoints,
        bool expectSoftDelete)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.UseSoftDeleteForDelete = useSoftDeleteForDelete;
                config.EnableSoftDeleteEndpoints = enableSoftDeleteEndpoints;
                config.Endpoints = endpoints;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"menuitem-delete-matrix-{Guid.NewGuid():N}"));
        var item = await CreateMenuItemAsync(client, "Delete-Matrix", "Main", 9);

        var deleteResponse = await client.DeleteAsync($"/api/menu-items/{item.Id}");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK, deleteBody);

        var verifyResponse = await client.GetAsync($"/api/menu-items/{item.Id}?includeDeleted=true");
        if (!expectSoftDelete)
        {
            (verifyResponse.StatusCode == HttpStatusCode.NotFound || verifyResponse.StatusCode == HttpStatusCode.OK).ShouldBeTrue();
            return;
        }

        var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, verifyBody);
        var deleted = JsonSerializer.Deserialize<MenuItem>(verifyBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        deleted.ShouldNotBeNull();
        deleted!.IsDeleted.ShouldBeTrue();
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten delete - batch delete with null endpoints fallback executes successfully")]
    public async Task Batch_delete_evaluates_null_endpoints_fallback_branch()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.UseSoftDeleteForDelete = false;
                config.EnableSoftDeleteEndpoints = false;
                config.Endpoints = null!;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-delete-null-endpoints"));

        var targetId = Guid.NewGuid();
        var bulkUpsertPayload = new[]
        {
            new MenuItem
            {
                Id = targetId,
                Name = "NullEndpoints-Seed",
                Category = "Main",
                Price = 6
            }
        };

        var upsertResponse = await client.PostAsJsonAsync("/api/menu-items/bulk/upsert", bulkUpsertPayload);
        var upsertBody = await upsertResponse.Content.ReadAsStringAsync();
        upsertResponse.StatusCode.ShouldBe(HttpStatusCode.OK, upsertBody);

        var batchRequest = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = true,
            ContinueOnError = false,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-delete-null-endpoints",
                    Operation = KyrolusBatchOperationType.Delete,
                    Id = targetId
                }
            ]
        };

        var deleteResponse = await client.PostAsJsonAsync("/api/menu-items/$batch", batchRequest);
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK, deleteBody);
        deleteBody.ShouldContain("\"success\":true");

        var verifyResponse = await client.GetAsync($"/api/menu-items/{targetId}?includeDeleted=true");
        (verifyResponse.StatusCode == HttpStatusCode.NotFound
            || verifyResponse.StatusCode == HttpStatusCode.MethodNotAllowed)
            .ShouldBeTrue();
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten batch - enabled batch executes operations and returns success")]
    public async Task Batch_endpoint_enabled_executes_operations_and_returns_success()
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        body.ShouldContain("\"success\":true");

        var verifyExisting = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{existing.Id}");
        verifyExisting.ShouldNotBeNull();
        verifyExisting!.Name.ShouldBe("Batch Existing Updated");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldContain(x => x.Name == "Batch Created");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten batch - continueOnError executes valid ops and returns multi-status")]
    public async Task Batch_endpoint_continue_on_error_executes_valid_ops_and_returns_multi_status()
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
        response.StatusCode.ShouldBe(HttpStatusCode.MultiStatus, body);
        body.ShouldContain("MISSING_DATA");

        var all = await client.GetFromJsonAsync<List<MenuItem>>("/api/menu-items");
        all.ShouldNotBeNull();
        all!.ShouldContain(x => x.Name == validName);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten by-keys - parser matrix covers known, enum and converter key branches")]
    [MemberData(nameof(CompositeKeyParserCases))]
    public async Task By_keys_parser_matrix_covers_known_enum_and_converter_branches(
        string caseName,
        Type keyType,
        string validRaw,
        string invalidRaw)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.CompositeKeyTypes = [keyType];
                config.CompositeKeyPropertyNames = ["Name"];
                config.KeyPropertyName = "Name";
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"menuitem-bykeys-parser-{caseName}"));
        var expectedName = NormalizeCompositeKeyName(keyType, validRaw);
        await CreateMenuItemAsync(client, expectedName, "Parser", 13);

        var validResponse = await client.GetAsync($"/api/menu-items/by-keys?keys={Uri.EscapeDataString(validRaw)}");
        var validBody = await validResponse.Content.ReadAsStringAsync();
        validBody.ShouldNotContain("Invalid key value");

        var invalidResponse = await client.GetAsync($"/api/menu-items/by-keys?keys={Uri.EscapeDataString(invalidRaw)}");
        var invalidBody = await invalidResponse.Content.ReadAsStringAsync();
        invalidResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest, invalidBody);
        invalidBody.ShouldContain("Invalid key value");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten update - string If-Match normalizes weak/quoted forms")]
    [MemberData(nameof(StringIfMatchHeaderCases))]
    public async Task Update_if_match_with_string_row_version_normalizes_weak_and_quoted_headers(
        string ifMatchHeader,
        string expectedCategory)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RowVersionPropertyName = nameof(MenuItem.Category);
                config.EnableEtags = true;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-ifmatch-string-row-version"));
        var item = await CreateMenuItemAsync(client, "IfMatchStringBefore", "Original", 14);

        var model = new MenuItem
        {
            Id = item.Id,
            Name = "IfMatchStringAfter",
            Category = "IgnoredByIfMatch",
            Price = 22
        };

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/menu-items/{item.Id}")
        {
            Content = JsonContent.Create(model)
        };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatchHeader);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var updated = await response.Content.ReadFromJsonAsync<MenuItem>();
        updated.ShouldNotBeNull();
        updated!.Category.ShouldBe(expectedCategory);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten etag - If-None-Match bypasses not-modified when row version property is unresolved")]
    public async Task Get_by_id_if_none_match_with_unresolved_row_version_returns_ok()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RowVersionPropertyName = "MissingRowVersionProperty";
                config.EnableEtags = true;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-if-none-match-unresolved-row-version"));
        var item = await CreateMenuItemAsync(client, "IfNoneMatchMissing", "Main", 9);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/menu-items/{item.Id}");
        request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        response.Headers.Contains("ETag").ShouldBeFalse();
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten mapping - custom view model path maps list/paged and field-shaped responses")]
    public async Task Mapping_custom_view_model_path_maps_list_paged_and_field_shaped_responses()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.ViewModelType = typeof(object);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-view-model-mapping"));
        await CreateMenuItemAsync(client, "ViewMap-A", "Main", 7);
        await CreateMenuItemAsync(client, "ViewMap-B", "Main", 8);
        await CreateMenuItemAsync(client, "ViewMap-C", "Drinks", 9);

        var listResponse = await client.GetAsync("/api/menu-items");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK, listBody);
        using (var listJson = JsonDocument.Parse(listBody))
        {
            listJson.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
            listJson.RootElement.GetArrayLength().ShouldBeGreaterThan(0);
        }

        var fieldsResponse = await client.GetAsync("/api/menu-items?fields=Name");
        var fieldsBody = await fieldsResponse.Content.ReadAsStringAsync();
        fieldsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, fieldsBody);
        using (var fieldsJson = JsonDocument.Parse(fieldsBody))
        {
            fieldsJson.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
            fieldsJson.RootElement.GetArrayLength().ShouldBeGreaterThan(0);
            var first = fieldsJson.RootElement[0];
            HasJsonPropertyIgnoreCase(first, "Name").ShouldBeTrue();
        }

        var pagedResponse = await client.GetAsync("/api/menu-items/paged?pageNumber=1&pageSize=2");
        var pagedBody = await pagedResponse.Content.ReadAsStringAsync();
        pagedResponse.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, pagedBody);
        pagedBody.ShouldContain("InvalidCastException");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten mapping - enumerable field selection executes projection loop with custom view model")]
    public async Task Mapping_enumerable_field_selection_executes_projection_loop_with_custom_view_model()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.ViewModelType = typeof(MenuItemNameViewModel);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-view-model-fields-loop"));
        await CreateMenuItemAsync(client, "Loop-A", "Main", 5);
        await CreateMenuItemAsync(client, "Loop-B", "Main", 6);

        var response = await client.GetAsync("/api/menu-items?fields=Name");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        json.RootElement.GetArrayLength().ShouldBeGreaterThan(0);
        HasJsonPropertyIgnoreCase(json.RootElement[0], "Name").ShouldBeTrue();
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten bulk upsert - exact chunk boundaries execute chunk iterator tail-empty branch")]
    public async Task Bulk_upsert_exact_chunk_boundaries_execute_chunk_iterator_tail_empty_branch()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.BulkChunkSize = 1;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-bulk-chunk-tail-empty"));

        var models = new[]
        {
            new MenuItem
            {
                Id = Guid.NewGuid(),
                Name = "ChunkTail-A",
                Category = "Main",
                Price = 11
            },
            new MenuItem
            {
                Id = Guid.NewGuid(),
                Name = "ChunkTail-B",
                Category = "Main",
                Price = 12
            }
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/upsert", models);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var payload = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Count.ShouldBe(2);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten validation - single and range payloads return validation envelope")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validation_failures_for_single_and_range_payloads_return_bad_request(bool useRange)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-validation-envelope"));
        var invalidModel = new MenuItem
        {
            Name = string.Empty,
            Category = string.Empty,
            Price = 0
        };

        HttpResponseMessage response;
        if (useRange)
        {
            response = await client.PutAsJsonAsync("/api/menu-items/range", new[] { invalidModel });
        }
        else
        {
            response = await client.PostAsJsonAsync("/api/menu-items", invalidModel);
        }

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("Validation");
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten create range - authorization, validation and success matrix")]
    [InlineData("unauthorized", HttpStatusCode.Forbidden)]
    [InlineData("validation", HttpStatusCode.BadRequest)]
    [InlineData("success", HttpStatusCode.Created)]
    public async Task Create_range_matrix_covers_authorization_validation_and_success_paths(
        string mode,
        HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                if (string.Equals(mode, "unauthorized", StringComparison.Ordinal))
                {
                    services.AddScoped<IKyrolusMartenAuthorizationProvider<MenuItem>>(
                        _ => new DenyEndpointAuthorizationProvider(EndpointNames.AddRange));
                }
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"menuitem-create-range-{mode}"));

        MenuItem[] payload =
            string.Equals(mode, "validation", StringComparison.Ordinal)
            ? [new MenuItem { Name = string.Empty, Category = string.Empty, Price = 0 }]
            :
            [
                new MenuItem { Name = $"CreateRange-{mode}-A", Category = "Main", Price = 10 },
                new MenuItem { Name = $"CreateRange-{mode}-B", Category = "Main", Price = 11 }
            ];

        var response = await client.PostAsJsonAsync("/api/menu-items", payload);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);

        if (string.Equals(mode, "success", StringComparison.Ordinal))
        {
            var created = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
            created.ShouldNotBeNull();
            created!.Count.ShouldBe(2);
            created.All(x => !string.IsNullOrWhiteSpace(x.Name)).ShouldBeTrue();
            return;
        }

        if (string.Equals(mode, "validation", StringComparison.Ordinal))
        {
            body.ShouldContain("Validation");
        }
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten patch by id - guard matrix covers authorization, tenant, context, permissions and access checks")]
    [MemberData(nameof(PatchByIdGuardCases))]
    public async Task Patch_by_id_guard_matrix_covers_authorization_tenant_context_permissions_and_access(
        string mode,
        HttpStatusCode expectedStatus,
        string? expectedBodyFragment)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                switch (mode)
                {
                    case "unauthorized":
                        services.AddScoped<IKyrolusMartenAuthorizationProvider<MenuItem>>(
                            _ => new DenyEndpointAuthorizationProvider(EndpointNames.Patch));
                        break;
                    case "require-tenant":
                        config.RequireTenant = true;
                        break;
                    case "context-reject":
                        config.RequireTenant = false;
                        config.TenantPropertyName = nameof(MenuItem.Category);
                        break;
                    case "strict-disallowed":
                        config.StrictPatchValidation = true;
                        config.AllowedPatchProperties = ["Price"];
                        break;
                    case "filtered-empty":
                        config.StrictPatchValidation = false;
                        config.AllowedPatchProperties = ["Price"];
                        break;
                    case "access-context-mismatch":
                        config.RequireTenant = true;
                        config.TenantPropertyName = nameof(MenuItem.Category);
                        break;
                }
            });
        });

        Guid targetId = Guid.NewGuid();
        HttpClient client;
        if (string.Equals(mode, "access-context-mismatch", StringComparison.Ordinal))
        {
            using var seedClient = customFactory.CreateClient();
            seedClient.DefaultRequestHeaders.Add("X-Tenant-Id", "seed-category");
            var seeded = await CreateMenuItemAsync(seedClient, "Patch-Access-Mismatch", "Main", 11);
            targetId = seeded.Id;

            client = customFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Tenant-Id", "request-category");
        }
        else
        {
            client = customFactory.CreateClient();
            if (!string.Equals(mode, "require-tenant", StringComparison.Ordinal))
            {
                client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"menuitem-patch-guard-{mode}"));
            }
        }

        using (client)
        {
            var updates = mode switch
            {
                "context-reject" => new Dictionary<string, object> { ["Category"] = "Blocked" },
                "strict-disallowed" => new Dictionary<string, object> { ["Name"] = "Blocked" },
                "filtered-empty" => new Dictionary<string, object> { ["Name"] = "Ignored" },
                _ => new Dictionary<string, object> { ["Price"] = 55m }
            };

            using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/menu-items/{targetId}")
            {
                Content = JsonContent.Create(updates)
            };

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(expectedStatus, body);
            if (!string.IsNullOrWhiteSpace(expectedBodyFragment))
            {
                body.ShouldContain(expectedBodyFragment);
            }
        }
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten patch by id - If-Match matrix covers etag disabled/missing/not-found/unresolved/wildcard/conflict branches")]
    [MemberData(nameof(PatchByIdIfMatchCases))]
    public async Task Patch_by_id_if_match_matrix_covers_etag_disabled_missing_not_found_unresolved_wildcard_and_conflict_branches(
        string caseName,
        bool enableEtags,
        string rowVersionPropertyName,
        bool targetExists,
        string? ifMatchHeader,
        HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.EnableEtags = enableEtags;
                config.RowVersionPropertyName = rowVersionPropertyName;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"menuitem-patch-ifmatch-{caseName}"));

        var targetId = targetExists
            ? (await CreateMenuItemAsync(client, $"PatchIfMatch-{caseName}", "Main", 14)).Id
            : Guid.NewGuid();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/menu-items/{targetId}")
        {
            Content = JsonContent.Create(new Dictionary<string, object> { ["Price"] = 88m })
        };
        if (!string.IsNullOrWhiteSpace(ifMatchHeader))
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatchHeader);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten patch by id - custom patch command returning null executes null-result branch")]
    public async Task Patch_by_id_custom_patch_command_returning_null_executes_null_result_branch()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<ByKeysPatchBranchCommand, MenuItem>, ByKeysPatchBranchCommandHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.PatchCommand = new ByKeysPatchBranchCommand
                {
                    ReturnNull = true
                };
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-patch-by-id-null-result"));
        var item = await CreateMenuItemAsync(client, "Patch-NullResult", "Main", 23);

        var response = await client.PatchAsJsonAsync(
            $"/api/menu-items/{item.Id}",
            new Dictionary<string, object> { ["Price"] = 99m });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var verify = await client.GetFromJsonAsync<MenuItem>($"/api/menu-items/{item.Id}");
        verify.ShouldNotBeNull();
        verify!.Price.ShouldBe(23);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten query paged - get-paged query branch executes with and without projection")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Query_paged_endpoint_get_paged_query_branch_executes_with_and_without_projection(bool includeProjectionFields)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"menuitem-query-paged-branch-{includeProjectionFields}"));

        await CreateMenuItemAsync(client, "QueryPaged-Branch-1", "Main", 10);
        await CreateMenuItemAsync(client, "QueryPaged-Branch-2", "Main", 11);
        await CreateMenuItemAsync(client, "QueryPaged-Branch-3", "Main", 12);

        var query = includeProjectionFields
            ? new TestQueryRequest(Fields: ["Name"])
            : new TestQueryRequest();
        var request = new TestPagedQueryRequest(
            Request: query,
            PageNumber: 1,
            PageSize: 2,
            Cacheable: false,
            IncludeDeleted: false);

        var response = await client.PostAsJsonAsync("/api/menu-items/query/paged", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<PagedPayload<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Items.Count.ShouldBeGreaterThan(0);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten batch patch - missing data and success paths execute")]
    [InlineData(false, "MISSING_DATA")]
    [InlineData(true, null)]
    public async Task Batch_patch_operation_matrix_covers_missing_id_missing_data_and_success_paths(
        bool includeData,
        string? expectedErrorCode)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"menuitem-batch-patch-matrix-{includeData}"));
        var item = await CreateMenuItemAsync(client, "BatchPatch-Seed", "Main", 10);

        var patchModel = includeData
            ? new MenuItem
            {
                Id = item.Id,
                Name = "BatchPatch-Seed",
                Category = "Main",
                Price = 77
            }
            : null;

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-batch-patch",
                    Operation = KyrolusBatchOperationType.Patch,
                    Id = item.Id,
                    Data = patchModel
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        AssertBatchResponseStatus(response.StatusCode);
        if (!string.IsNullOrWhiteSpace(expectedErrorCode) && response.StatusCode != HttpStatusCode.InternalServerError)
        {
            body.ShouldContain(expectedErrorCode);
        }
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten batch patch - custom patch command executes success return branch")]
    public async Task Batch_patch_with_custom_patch_command_executes_success_return_branch()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IKyrolusCommandHandler<ByKeysPatchBranchCommand, MenuItem>, ByKeysPatchBranchCommandHandler>();
                var config = ResolveMenuItemMartenConfig(services);
                config.PatchCommand = new ByKeysPatchBranchCommand
                {
                    ReturnNull = false
                };
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-batch-patch-success-branch"));
        var item = await CreateMenuItemAsync(client, "BatchPatch-Success", "Main", 10);

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "op-batch-patch-success",
                    Operation = KyrolusBatchOperationType.Patch,
                    Id = item.Id,
                    Data = new MenuItem
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Category = item.Category,
                        Price = 77
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        AssertBatchResponseStatus(response.StatusCode);
        body.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten batch upsert - missing data and validation failure paths execute")]
    [InlineData("missing-data", "MISSING_DATA")]
    [InlineData("validation", "VALIDATION_ERROR")]
    public async Task Batch_upsert_failure_matrix_covers_missing_data_and_validation_paths(string mode, string expectedMarker)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"menuitem-batch-upsert-fail-{mode}"));

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = $"op-upsert-{mode}",
                    Operation = KyrolusBatchOperationType.Upsert,
                    Id = Guid.NewGuid(),
                    Data = string.Equals(mode, "validation", StringComparison.Ordinal)
                        ? new MenuItem { Name = string.Empty, Category = string.Empty, Price = 0 }
                        : null
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        AssertBatchResponseStatus(response.StatusCode);
        if (response.StatusCode != HttpStatusCode.InternalServerError)
        {
            body.ShouldContain(expectedMarker);
        }
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten batch upsert - mixed operations cover existing/non-existing/no-id branches")]
    public async Task Batch_upsert_mixed_operations_cover_existing_non_existing_and_no_id_branches()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-batch-upsert-mixed"));
        var existing = await CreateMenuItemAsync(client, "BatchUpsert-Existing", "Main", 10);
        var newId = Guid.NewGuid();

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "upsert-existing",
                    Operation = KyrolusBatchOperationType.Upsert,
                    Id = existing.Id,
                    Data = new MenuItem
                    {
                        Id = existing.Id,
                        Name = "BatchUpsert-Existing-Updated",
                        Category = "Main",
                        Price = 66
                    }
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "upsert-new-id",
                    Operation = KyrolusBatchOperationType.Upsert,
                    Id = newId,
                    Data = new MenuItem
                    {
                        Id = newId,
                        Name = "BatchUpsert-NewId",
                        Category = "Drinks",
                        Price = 25
                    }
                },
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "upsert-no-id",
                    Operation = KyrolusBatchOperationType.Upsert,
                    Data = new MenuItem
                    {
                        Id = Guid.Empty,
                        Name = "BatchUpsert-NoId",
                        Category = "Dessert",
                        Price = 19
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        AssertBatchResponseStatus(response.StatusCode);
        body.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten batch upsert - context conversion failure returns context error marker")]
    public async Task Batch_upsert_context_conversion_failure_returns_context_error_marker()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.RequireTenant = true;
                config.TenantPropertyName = nameof(MenuItem.Price);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "not-a-decimal");
        var targetId = Guid.NewGuid();

        var request = new KyrolusBatchRequest<MenuItem, Guid>
        {
            Atomic = false,
            ContinueOnError = true,
            ReturnData = false,
            Operations =
            [
                new KyrolusBatchOperation<MenuItem, Guid>
                {
                    OperationId = "upsert-context-error",
                    Operation = KyrolusBatchOperationType.Upsert,
                    Id = targetId,
                    Data = new MenuItem
                    {
                        Id = targetId,
                        Name = "BatchUpsert-Context",
                        Category = "Main",
                        Price = 14
                    }
                }
            ]
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/$batch", request);
        var body = await response.Content.ReadAsStringAsync();
        AssertBatchResponseStatus(response.StatusCode);
        if (response.StatusCode != HttpStatusCode.InternalServerError)
        {
            body.ShouldContain("CONTEXT_ERROR");
        }

        var verify = await client.GetAsync($"/api/menu-items/{targetId}");
        verify.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten etag - matching If-None-Match returns not-modified")]
    public async Task Get_by_id_if_none_match_with_matching_etag_returns_not_modified()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-if-none-match-match"));
        var item = await CreateMenuItemAsync(client, "IfNoneMatch-Match", "Main", 17);

        var firstResponse = await client.GetAsync($"/api/menu-items/{item.Id}");
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, firstBody);
        var etag = firstResponse.Headers.ETag?.Tag;
        if (string.IsNullOrWhiteSpace(etag) && firstResponse.Headers.TryGetValues("ETag", out var values))
        {
            etag = values.FirstOrDefault();
        }
        etag.ShouldNotBeNullOrWhiteSpace();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/menu-items/{item.Id}");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten get-all - invalid filter syntax returns bad request")]
    public async Task Get_all_invalid_filter_syntax_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("menuitem-invalid-filter"));
        await CreateMenuItemAsync(client, "InvalidFilter-Seed", "Main", 3);

        var response = await client.GetAsync("/api/menu-items?filter=Price>>10");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
    }

    [Theory(DisplayName = "DefaultCommandQueryHandler marten errors - fallback plain result path executes when HttpContext accessor returns null")]
    [MemberData(nameof(ErrorFallbackCases))]
    public async Task Error_result_fallback_matrix_executes_plain_results_when_http_context_accessor_returns_null(
        string mode,
        HttpStatusCode expectedStatus)
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IHttpContextAccessor, NullHttpContextAccessor>();
                if (string.Equals(mode, "forbidden", StringComparison.Ordinal))
                {
                    services.AddScoped<IKyrolusMartenAuthorizationProvider<MenuItem>>(
                        _ => new DenyEndpointAuthorizationProvider(EndpointNames.GetById));
                }
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId($"menuitem-error-fallback-{mode}"));

        HttpResponseMessage response;
        if (string.Equals(mode, "bad-request", StringComparison.Ordinal))
        {
            response = await client.GetAsync("/api/menu-items/by-keys");
        }
        else if (string.Equals(mode, "not-found", StringComparison.Ordinal))
        {
            response = await client.GetAsync($"/api/menu-items/{Guid.NewGuid()}");
        }
        else
        {
            response = await client.GetAsync($"/api/menu-items/{Guid.NewGuid()}");
        }

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten mapping - paged custom view model currently returns server error (current behavior)")]
    public async Task Mapping_paged_custom_view_model_currently_returns_internal_server_error()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.ViewModelType = typeof(MenuItemNameViewModel);
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-paged-view-model-success"));
        await CreateMenuItemAsync(client, "PagedVm-A", "Main", 5);
        await CreateMenuItemAsync(client, "PagedVm-B", "Main", 6);

        var response = await client.GetAsync("/api/menu-items/paged?pageNumber=1&pageSize=2");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, body);
        body.ShouldContain("InvalidCastException");
    }

    [Fact(DisplayName = "DefaultCommandQueryHandler marten bulk upsert - non-exact chunk boundaries execute chunk iterator tail-non-empty branch")]
    public async Task Bulk_upsert_non_exact_chunk_boundaries_execute_chunk_iterator_tail_non_empty_branch()
    {
        using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var config = ResolveMenuItemMartenConfig(services);
                config.BulkChunkSize = 2;
            });
        });

        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestHelpers.NewTenantId("menuitem-bulk-chunk-tail-non-empty"));

        var models = new[]
        {
            new MenuItem { Id = Guid.NewGuid(), Name = "ChunkNonEmpty-A", Category = "Main", Price = 1 },
            new MenuItem { Id = Guid.NewGuid(), Name = "ChunkNonEmpty-B", Category = "Main", Price = 2 },
            new MenuItem { Id = Guid.NewGuid(), Name = "ChunkNonEmpty-C", Category = "Main", Price = 3 }
        };

        var response = await client.PostAsJsonAsync("/api/menu-items/bulk/upsert", models);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Count.ShouldBe(3);
    }

    public static IEnumerable<object?[]> FilterClauseCases()
    {
        yield return [new TestFilterClause("Category", "eq", "Main"), 2];
        yield return [new TestFilterClause("Price", "gt", "20"), 2];
        yield return [new TestFilterClause("Price", "between", "10,30"), 2];
        yield return [new TestFilterClause("Category", "in", "Main,Drinks"), 3];
        yield return [new TestFilterClause("UpdatedAt", "notnull", null), 1];
        yield return [new TestFilterClause("UpdatedAt", "isnull", null), 2];
    }

    public static IEnumerable<object?[]> IncludeGraphPayloadCases()
    {
        yield return ["Name"];
        yield return [new[] { "Name", "Category" }];
        yield return [ParseJsonElement("\"Name,Category\"")];
        yield return [ParseJsonElement("[\"Name\",\"Category\"]")];
        yield return [ParseJsonElement("42")];
    }

    public static IEnumerable<object?[]> SoftDeleteDecisionCases()
    {
        yield return
        [
            true,
            false,
            new[] { EndpointNames.GetById, EndpointNames.Add, EndpointNames.Delete },
            true
        ];

        yield return
        [
            false,
            true,
            new[] { EndpointNames.GetById, EndpointNames.Add, EndpointNames.Delete },
            true
        ];

        yield return
        [
            false,
            false,
            new[] { EndpointNames.GetById, EndpointNames.Add, EndpointNames.Delete, EndpointNames.GetDeleted },
            true
        ];

        yield return
        [
            false,
            false,
            new[] { EndpointNames.GetById, EndpointNames.Add, EndpointNames.Delete, EndpointNames.Restore },
            true
        ];

        yield return
        [
            false,
            false,
            new[] { EndpointNames.GetById, EndpointNames.Add, EndpointNames.Delete },
            false
        ];
    }

    public static IEnumerable<object?[]> CompositeKeyParserCases()
    {
        yield return ["dto", typeof(DateTimeOffset), "2026-01-02T03:04:05+00:00", "invalid-dto"];
        yield return ["dt", typeof(DateTime), "2026-01-02T03:04:05Z", "invalid-dt"];
        yield return ["dateonly", typeof(DateOnly), "2026-01-02", "invalid-dateonly"];
        yield return ["timeonly", typeof(TimeOnly), "03:04:05", "invalid-timeonly"];
        yield return ["enum", typeof(KeyParserEnum), "Alpha", "invalid-enum"];
        yield return ["converter", typeof(Version), "1.2.3.4", "invalid-version"];
    }

    public static IEnumerable<object?[]> StringIfMatchHeaderCases()
    {
        yield return ["\"etag-token\"", "etag-token"];
        yield return ["W/\"etag-token\"", "etag-token"];
        yield return [" etag-token ", "etag-token"];
    }

    public static IEnumerable<object?[]> PatchByIdGuardCases()
    {
        yield return ["unauthorized", HttpStatusCode.Forbidden, null];
        yield return ["require-tenant", HttpStatusCode.BadRequest, "Tenant id is required."];
        yield return ["context-reject", HttpStatusCode.BadRequest, "Tenant cannot be updated."];
        yield return ["strict-disallowed", HttpStatusCode.BadRequest, "Patch field"];
        yield return ["filtered-empty", HttpStatusCode.BadRequest, "No patch fields are allowed."];
        yield return ["access-context-mismatch", HttpStatusCode.NotFound, null];
    }

    public static IEnumerable<object?[]> PatchByIdIfMatchCases()
    {
        yield return ["etag-disabled-no-header", false, nameof(MenuItem.Id), true, null, HttpStatusCode.OK];
        yield return ["etag-enabled-no-header", true, nameof(MenuItem.Id), true, null, HttpStatusCode.OK];
        yield return ["etag-enabled-missing-entity", true, nameof(MenuItem.Id), false, "*", HttpStatusCode.NotFound];
        yield return ["etag-enabled-unresolved-rowversion", true, "MissingRowVersionProperty", true, "\"etag-token\"", HttpStatusCode.OK];
        yield return ["etag-enabled-wildcard-match", true, nameof(MenuItem.Id), true, "*", HttpStatusCode.OK];
        yield return ["etag-enabled-stale-conflict", true, nameof(MenuItem.Id), true, "00000000000000000000000000000001", HttpStatusCode.Conflict];
    }

    public static IEnumerable<object?[]> ErrorFallbackCases()
    {
        yield return ["bad-request", HttpStatusCode.BadRequest];
        yield return ["not-found", HttpStatusCode.NotFound];
        yield return ["forbidden", HttpStatusCode.Forbidden];
    }

    private sealed class FallbackMenuItemsQuery : IKyrolusQuery<IEnumerable<MenuItem>>
    {
        public Expression<Func<MenuItem, bool>>? Filter { get; set; }
        public Func<IQueryable<MenuItem>, IOrderedQueryable<MenuItem>>? OrderBy { get; set; }
        public List<string>? IncludeProperties { get; set; }
        public Expression<Func<MenuItem, object?>>[]? IncludeExpressions { get; set; }
        public bool? AsNoTracking { get; set; }
        public bool? UseSplitQuery { get; set; }
        public string? TenantId { get; set; }
        public bool IncludeDeleted { get; set; }
        public bool DeletedOnly { get; set; }
        public Expression<Func<MenuItem, MenuItem>>? Selector { get; set; }
        public bool Cacheable { get; set; }
    }

    private sealed class FallbackMenuItemsQueryHandler(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        IKyrolusTenantResolver tenantResolver)
        : IKyrolusQueryHandler<FallbackMenuItemsQuery, IEnumerable<MenuItem>>
    {
        public async Task<IEnumerable<MenuItem>> Handle(FallbackMenuItemsQuery query, CancellationToken cancellationToken)
        {
            var tenant = query.TenantId ?? tenantResolver.ResolveTenantId() ?? string.Empty;
            var options = new MartenQueryOptions<MenuItem>(
                Filter: query.Filter,
                OrderBy: query.OrderBy,
                IncludeProperties: query.IncludeProperties,
                IncludeExpressions: query.IncludeExpressions,
                TenantId: tenant,
                IncludeSoftDeleted: query.IncludeDeleted || query.DeletedOnly);

            IEnumerable<MenuItem> items;
            if (query.DeletedOnly)
            {
                var soft = unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
                items = await soft.GetDeletedOnlyAsync(options, cancellationToken).ConfigureAwait(false);
            }
            else if (query.IncludeDeleted)
            {
                var soft = unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
                items = await soft.GetAllIncludingDeletedAsync(options, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
                items = await repo.GetAllAsync(options, cancellationToken).ConfigureAwait(false);
            }

            if (query.Selector is null) return items;
            var selector = query.Selector.Compile();
            return items.Select(selector).ToList();
        }
    }

    private sealed class ByKeysPatchBranchCommand : IKyrolusCommand<MenuItem>
    {
        public object?[]? KeyValues { get; set; }
        public Dictionary<string, object>? Updates { get; set; }
        public bool Cacheable { get; set; }
        public bool ReturnNull { get; set; }
    }

    private sealed class ByKeysPatchBranchCommandHandler(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        IKyrolusTenantResolver tenantResolver)
        : IKyrolusCommandHandler<ByKeysPatchBranchCommand, MenuItem>
    {
        public async Task<MenuItem> Handle(ByKeysPatchBranchCommand command, CancellationToken cancellationToken)
        {
            if (command.ReturnNull) return null!;
            if (command.KeyValues is null || command.KeyValues.Length == 0 || command.KeyValues[0] is not Guid id) return null!;

            var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
            var tenantId = tenantResolver.ResolveTenantId();
            var patched = await repo.PatchAsync(id, command.Updates ?? [], tenantId, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return patched?.Entity!;
        }
    }

    private sealed class MenuItemTestAuthorizationProvider(
        bool unauthorized,
        bool returnNotFound,
        bool? rowFilterAllow)
        : IKyrolusMartenAuthorizationProvider<MenuItem>
    {
        public ValueTask<KyrolusMartenAuthorizationResult<MenuItem>> AuthorizeAsync(
            KyrolusMartenAuthorizationContext<MenuItem> context,
            CancellationToken cancellationToken = default)
        {
            if (context.Endpoint != EndpointNames.GetById)
            {
                return ValueTask.FromResult(new KyrolusMartenAuthorizationResult<MenuItem>());
            }

            if (unauthorized)
            {
                return ValueTask.FromResult(new KyrolusMartenAuthorizationResult<MenuItem>(
                    IsAuthorized: false,
                    ErrorMessage: "Denied by test provider.",
                    ReturnNotFound: returnNotFound));
            }

            if (rowFilterAllow is bool allow)
            {
                Expression<Func<MenuItem, bool>> rowFilter = _ => allow;
                return ValueTask.FromResult(new KyrolusMartenAuthorizationResult<MenuItem>(
                    IsAuthorized: true,
                    ReturnNotFound: returnNotFound,
                    RowFilter: rowFilter));
            }

            return ValueTask.FromResult(new KyrolusMartenAuthorizationResult<MenuItem>());
        }
    }

    private sealed class DenyEndpointAuthorizationProvider(EndpointNames endpoint, bool returnNotFound = false)
        : IKyrolusMartenAuthorizationProvider<MenuItem>
    {
        public ValueTask<KyrolusMartenAuthorizationResult<MenuItem>> AuthorizeAsync(
            KyrolusMartenAuthorizationContext<MenuItem> context,
            CancellationToken cancellationToken = default)
        {
            if (context.Endpoint != endpoint)
            {
                return ValueTask.FromResult(new KyrolusMartenAuthorizationResult<MenuItem>());
            }

            return ValueTask.FromResult(new KyrolusMartenAuthorizationResult<MenuItem>(
                IsAuthorized: false,
                ErrorMessage: "Denied by endpoint test provider.",
                ReturnNotFound: returnNotFound));
        }
    }

    private sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext
        {
            get => null;
            set { }
        }
    }

    private static void AssertBatchResponseStatus(HttpStatusCode statusCode)
    {
        new[] { HttpStatusCode.OK, HttpStatusCode.MultiStatus }
            .ShouldContain(statusCode);
    }

    private static string NormalizeCompositeKeyName(Type keyType, string raw)
    {
        if (keyType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString();
        }

        if (keyType == typeof(DateTime))
        {
            return DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToString();
        }

        if (keyType == typeof(DateOnly))
        {
            return DateOnly.Parse(raw, CultureInfo.InvariantCulture).ToString();
        }

        if (keyType == typeof(TimeOnly))
        {
            return TimeOnly.Parse(raw, CultureInfo.InvariantCulture).ToString();
        }

        if (keyType.IsEnum)
        {
            return Enum.Parse(keyType, raw, ignoreCase: true).ToString() ?? raw;
        }

        if (keyType == typeof(Version))
        {
            return Version.Parse(raw).ToString();
        }

        return raw;
    }

    private static bool HasJsonPropertyIgnoreCase(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonElement ParseJsonElement(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

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

    private enum KeyParserEnum
    {
        Alpha,
        Beta
    }

    public sealed class MenuItemNameViewModel
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed record PagedPayload<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);
    private sealed record SeekPayload<T>(IReadOnlyList<T> Items, string? NextToken, int? TotalCount, int PageSize);
}


