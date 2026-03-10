using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json.Serialization;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class MartenRuntimeQueryHelperIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Theory(DisplayName = "Marten query helper - supports scalar filter operators")]
    [MemberData(nameof(ScalarFilterCases))]
    public async Task Query_helper_supports_scalar_filter_operators(QueryHelperFilter filter, int expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-menu-filter"));
        await SeedMenuItemsAsync(client);

        var request = new QueryHelperRequest(Filters: [filter]);
        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(expectedCount, body);
    }

    [Theory(DisplayName = "Marten query helper - invalid scalar filters return 400")]
    [MemberData(nameof(InvalidScalarFilterCases))]
    public async Task Query_helper_invalid_scalar_filters_return_bad_request(QueryHelperFilter filter)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-menu-invalid"));
        await SeedMenuItemsAsync(client);

        var request = new QueryHelperRequest(Filters: [filter]);
        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/query-helper", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Marten query helper - supports multi-column ordering")]
    public async Task Query_helper_supports_multi_column_ordering()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-menu-order"));
        await SeedMenuItemsAsync(client);

        var request = new QueryHelperRequest(
            OrderBy:
            [
                new QueryHelperOrder("Category", Desc: false),
                new QueryHelperOrder("Price", Desc: true)
            ]);

        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(3);
        items[0].Category.ShouldBe("Drinks");

        var mainItems = items.Where(x => x.Category == "Main").ToList();
        mainItems.Count.ShouldBe(2);
        mainItems[0].Price.ShouldBeGreaterThan(mainItems[1].Price);
    }

    [Theory(DisplayName = "Marten query helper - supports any/all nested filters")]
    [InlineData("any", 1)]
    [InlineData("all", 0)]
    public async Task Query_helper_supports_any_all_nested_filters(string op, int expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-orders-anyall"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        await CreateOrderAsync(
            client,
            "one@local.test",
            new()
            {
                new OrderLine { MenuItemId = Guid.NewGuid(), Name = "LowQty", UnitPrice = 10, Quantity = 1 }
            });

        await CreateOrderAsync(
            client,
            "two@local.test",
            new()
            {
                new OrderLine { MenuItemId = Guid.NewGuid(), Name = "HighQty1", UnitPrice = 10, Quantity = 3 },
                new OrderLine { MenuItemId = Guid.NewGuid(), Name = "HighQty2", UnitPrice = 12, Quantity = 4 }
            });

        var request = new QueryHelperRequest(Filters: [new QueryHelperFilter("Lines", op, "Quantity>1")]);
        var response = await client.PostAsJsonAsync("/api/orders/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        orders.ShouldNotBeNull();
        orders!.Count.ShouldBe(expectedCount, body);
    }

    [Theory(DisplayName = "Marten query helper - menu diagnostics request matrix covers success and failure paths")]
    [MemberData(nameof(MenuQueryRequestCases))]
    public async Task Query_helper_menu_diagnostics_request_matrix_covers_success_and_failure_paths(
        QueryHelperRequest? request,
        HttpStatusCode expectedStatus,
        int? expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-menu-matrix"));
        await SeedMenuItemsAsync(client);

        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);

        if (expectedStatus != HttpStatusCode.OK || expectedCount is null)
        {
            return;
        }

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(expectedCount.Value, body);
    }

    [Fact(DisplayName = "Marten query helper - supports single-column descending ordering")]
    public async Task Query_helper_supports_single_column_descending_ordering()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-menu-order-desc"));
        await SeedMenuItemsAsync(client);

        var request = new QueryHelperRequest(OrderBy: [new QueryHelperOrder("Price", Desc: true)]);
        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Select(x => x.Price).ShouldBe([40m, 26m, 10m], body);
    }

    [Fact(DisplayName = "Marten query helper - parses Guid filter values for key properties")]
    public async Task Query_helper_parses_guid_filter_values_for_key_properties()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-menu-guid"));
        var target = await CreateMenuItemAsync(client, "GuidTarget", "Main", 15);
        await CreateMenuItemAsync(client, "Other", "Main", 20);

        var request = new QueryHelperRequest(Filters: [new QueryHelperFilter("Id", "eq", target.Id.ToString("D", CultureInfo.InvariantCulture))]);
        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(1, body);
        items[0].Id.ShouldBe(target.Id, body);
    }

    [Fact(DisplayName = "Marten query helper - parses enum filter values case-insensitively")]
    public async Task Query_helper_parses_enum_filter_values_case_insensitively()
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-orders-enum"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        await CreateOrderAsync(
            client,
            "enum@local.test",
            [new OrderLine { MenuItemId = Guid.NewGuid(), Name = "EnumCase", UnitPrice = 20, Quantity = 2 }]);

        var request = new QueryHelperRequest(Filters: [new QueryHelperFilter("Status", "eq", "paid")]);
        var response = await client.PostAsJsonAsync("/api/orders/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        orders.ShouldNotBeNull();
        orders!.Count.ShouldBe(1, body);
        orders[0].Status.ShouldBe(OrderStatus.Paid, body);
    }

    [Theory(DisplayName = "Marten query helper - order lines any/all matrix covers nested and invalid branches")]
    [MemberData(nameof(OrderLineAnyAllMatrixCases))]
    public async Task Query_helper_order_lines_any_all_matrix_covers_nested_and_invalid_branches(
        QueryHelperFilter filter,
        HttpStatusCode expectedStatus,
        int? expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-helper-orders-anyall-matrix"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        await SeedOrdersForAnyAllMatrixAsync(client);

        var request = new QueryHelperRequest(Filters: [filter]);
        var response = await client.PostAsJsonAsync("/api/orders/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);

        if (expectedStatus != HttpStatusCode.OK || expectedCount is null)
        {
            return;
        }

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        orders.ShouldNotBeNull();
        orders!.Count.ShouldBe(expectedCount.Value, body);
    }

    [Theory(DisplayName = "Marten query helper - bulk menu matrix covers aliases nullable and parser guards")]
    [MemberData(nameof(MenuBulkBurstCases))]
    public async Task Query_helper_bulk_menu_matrix_covers_aliases_nullable_and_parser_guards(
        string caseName,
        QueryHelperFilter[] filtersTemplate,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"marten-helper-menu-burst-{NormalizeCaseName(caseName)}"));
        var seeded = await SeedMenuItemsForBurstMatrixAsync(client);
        var filters = ReplaceMenuTokens(filtersTemplate, seeded);
        var request = new QueryHelperRequest(Filters: filters);

        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);

        if (expectedFragment is not null)
        {
            body.ShouldContain(expectedFragment);
        }

        if (expectedStatus != HttpStatusCode.OK || expectedCount is null)
        {
            return;
        }

        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        items!.Count.ShouldBe(expectedCount.Value, body);
    }

    [Theory(DisplayName = "Marten query helper - bulk orders matrix covers collection and temporal branches")]
    [MemberData(nameof(OrderBulkBurstCases))]
    public async Task Query_helper_bulk_orders_matrix_covers_collection_and_temporal_branches(
        string caseName,
        QueryHelperFilter[] filtersTemplate,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"marten-helper-orders-burst-{NormalizeCaseName(caseName)}"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);
        var seeded = await SeedOrdersForBurstMatrixAsync(client);
        var filters = ReplaceOrderTokens(filtersTemplate, seeded);
        var request = new QueryHelperRequest(Filters: filters);

        var response = await client.PostAsJsonAsync("/api/orders/diagnostics/query-helper", request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expectedStatus, body);

        if (expectedFragment is not null)
        {
            body.ShouldContain(expectedFragment);
        }

        if (expectedStatus != HttpStatusCode.OK || expectedCount is null)
        {
            return;
        }

        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        orders.ShouldNotBeNull();
        orders!.Count.ShouldBe(expectedCount.Value, body);
    }

    public static IEnumerable<object[]> ScalarFilterCases()
    {
        yield return [new QueryHelperFilter("Category", "eq", "Main"), 2];
        yield return [new QueryHelperFilter("Price", "gt", "20"), 2];
        yield return [new QueryHelperFilter("Price", "between", "10..30"), 2];
        yield return [new QueryHelperFilter("Name", "in", "Alpha|Cola"), 2];
        yield return [new QueryHelperFilter("UpdatedAt", "eq", null), 2];
        yield return [new QueryHelperFilter("UpdatedAt", "neq", null), 1];
        yield return [new QueryHelperFilter("Name", "contains", "ol"), 1];
        yield return [new QueryHelperFilter("Name", "startswith", "Al"), 1];
        yield return [new QueryHelperFilter("Name", "endswith", "a"), 3];
    }

    public static IEnumerable<object[]> InvalidScalarFilterCases()
    {
        yield return [new QueryHelperFilter("Unknown", "eq", "x")];
        yield return [new QueryHelperFilter("Name", "any", "x")];
        yield return [new QueryHelperFilter("Price", "isnull", null)];
        yield return [new QueryHelperFilter("Price", "gt", "abc")];
    }

    public static IEnumerable<object[]> MenuQueryRequestCases()
    {
        yield return [null, HttpStatusCode.OK, 3];
        yield return [new QueryHelperRequest(), HttpStatusCode.OK, 3];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Category", "=", "Main")]), HttpStatusCode.OK, 2];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Category", "<>", "Main")]), HttpStatusCode.OK, 1];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Price", ">=", "26")]), HttpStatusCode.OK, 2];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Price", "<", "26")]), HttpStatusCode.OK, 1];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Price", "in", "10|26")]), HttpStatusCode.OK, 2];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("UpdatedAt", "in", "null")]), HttpStatusCode.OK, 2];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("UpdatedAt", "isnull", null)]), HttpStatusCode.OK, 2];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("UpdatedAt", "notnull", null)]), HttpStatusCode.OK, 1];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("UpdatedAt", "eq", "null")]), HttpStatusCode.OK, 2];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("UpdatedAt", "neq", "null")]), HttpStatusCode.OK, 1];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("UpdatedAt", "between", "2000-01-01..2100-01-01")]), HttpStatusCode.OK, 1];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("CreatedAt", "between", "2000-01-01..2100-01-01")]), HttpStatusCode.OK, 3];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Name", "contains", "ol")]), HttpStatusCode.OK, 1];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Name", "startswith", "C")]), HttpStatusCode.OK, 1];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Name", "endswith", "a")]), HttpStatusCode.OK, 3];
        yield return
        [
            new QueryHelperRequest(
                Filters:
                [
                    new QueryHelperFilter("Price", "gt", "10"),
                    new QueryHelperFilter("Category", "eq", "Main")
                ]),
            HttpStatusCode.OK,
            1
        ];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("", "eq", "x")]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Name", "", "x")]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Unknown", "eq", "x")]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Price", "isnull", null)]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Price", "gt", "abc")]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Name", "between", "A..Z")]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("UpdatedAt", "gt", null)]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Name", "any", "x")]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(Filters: [new QueryHelperFilter("Price", "in", "10|null")]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(OrderBy: [new QueryHelperOrder("Unknown")]), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperRequest(OrderBy: [new QueryHelperOrder("", Desc: true)]), HttpStatusCode.BadRequest, (int?)null];
    }

    public static IEnumerable<object[]> OrderLineAnyAllMatrixCases()
    {
        yield return [new QueryHelperFilter("Lines", "any", "Quantity>2"), HttpStatusCode.OK, 1];
        yield return [new QueryHelperFilter("Lines", "any", "Quantity>=2"), HttpStatusCode.OK, 2];
        yield return [new QueryHelperFilter("Lines", "all", "Quantity>=2"), HttpStatusCode.OK, 2];
        yield return [new QueryHelperFilter("Lines", "all", "Quantity>=3"), HttpStatusCode.OK, 2];
        yield return [new QueryHelperFilter("Lines", "any", "Quantity between (2,2)"), HttpStatusCode.OK, 1];
        yield return [new QueryHelperFilter("Lines", "all", "Quantity between (1,4)"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperFilter("Lines", "any", "Quantity in (1,4)"), HttpStatusCode.OK, 2];
        yield return [new QueryHelperFilter("Lines", "all", "Quantity in (1,2,3,4)"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperFilter("Lines", "any", "Name contains \"High\""), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperFilter("Lines", "any", "Name startswith \"M\""), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperFilter("Lines", "any", "Unknown==1"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperFilter("CustomerEmail", "any", "admin"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperFilter("Lines", "any", "Quantity isnull"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperFilter("Lines", "any", "first,second"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new QueryHelperFilter("Lines", "all", "Quantity>"), HttpStatusCode.BadRequest, (int?)null];
    }

    public static IEnumerable<object[]> MenuBulkBurstCases()
    {
        var eqOps = new[] { "eq", "==", "=" };
        var neqOps = new[] { "neq", "!=", "<>" };
        var gtOps = new[] { "gt", ">" };
        var gteOps = new[] { "gte", ">=" };
        var ltOps = new[] { "lt", "<" };
        var lteOps = new[] { "lte", "<=" };

        foreach (var op in eqOps)
        {
            yield return MenuBurstCase($"category-main-{OperatorToken(op)}", [new("Category", op, "Main")], HttpStatusCode.OK, 2);
            yield return MenuBurstCase($"name-alpha-{OperatorToken(op)}", [new("Name", op, "Alpha")], HttpStatusCode.OK, 1);
            yield return MenuBurstCase($"id-alpha-{OperatorToken(op)}", [new("Id", op, "{alphaId}")], HttpStatusCode.OK, 1);
            yield return MenuBurstCase($"updatedat-null-{OperatorToken(op)}", [new("UpdatedAt", op, null)], HttpStatusCode.OK, 2);
            yield return MenuBurstCase($"isdeleted-false-{OperatorToken(op)}", [new("IsDeleted", op, "false")], HttpStatusCode.OK, 2);
        }

        foreach (var op in neqOps)
        {
            yield return MenuBurstCase($"category-not-main-{OperatorToken(op)}", [new("Category", op, "Main")], HttpStatusCode.OK, 1);
            yield return MenuBurstCase($"name-not-alpha-{OperatorToken(op)}", [new("Name", op, "Alpha")], HttpStatusCode.OK, 2);
            yield return MenuBurstCase($"id-not-alpha-{OperatorToken(op)}", [new("Id", op, "{alphaId}")], HttpStatusCode.OK, 2);
            yield return MenuBurstCase($"updatedat-not-null-{OperatorToken(op)}", [new("UpdatedAt", op, null)], HttpStatusCode.OK, 1);
            yield return MenuBurstCase($"isdeleted-not-false-{OperatorToken(op)}", [new("IsDeleted", op, "false")], HttpStatusCode.OK, 1);
        }

        foreach (var op in gtOps)
        {
            yield return MenuBurstCase($"price-gt20-{OperatorToken(op)}", [new("Price", op, "20")], HttpStatusCode.OK, 2);
            yield return MenuBurstCase($"createdat-gt-past-{OperatorToken(op)}", [new("CreatedAt", op, "2000-01-01T00:00:00Z")], HttpStatusCode.OK, 3);
        }

        foreach (var op in gteOps)
        {
            yield return MenuBurstCase($"price-gte26-{OperatorToken(op)}", [new("Price", op, "26")], HttpStatusCode.OK, 2);
            yield return MenuBurstCase($"createdat-gte-past-{OperatorToken(op)}", [new("CreatedAt", op, "2000-01-01T00:00:00Z")], HttpStatusCode.OK, 3);
        }

        foreach (var op in ltOps)
        {
            yield return MenuBurstCase($"price-lt26-{OperatorToken(op)}", [new("Price", op, "26")], HttpStatusCode.OK, 1);
            yield return MenuBurstCase($"createdat-lt-future-{OperatorToken(op)}", [new("CreatedAt", op, "2100-01-01T00:00:00Z")], HttpStatusCode.OK, 3);
        }

        foreach (var op in lteOps)
        {
            yield return MenuBurstCase($"price-lte10-{OperatorToken(op)}", [new("Price", op, "10")], HttpStatusCode.OK, 1);
            yield return MenuBurstCase($"createdat-lte-future-{OperatorToken(op)}", [new("CreatedAt", op, "2100-01-01T00:00:00Z")], HttpStatusCode.OK, 3);
        }

        yield return MenuBurstCase("updatedat-isnull", [new("UpdatedAt", "isnull", null)], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("updatedat-notnull", [new("UpdatedAt", "notnull", null)], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("name-contains-ol", [new("Name", "contains", "ol")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("name-startswith-a", [new("Name", "startswith", "A")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("name-endswith-a", [new("Name", "endswith", "a")], HttpStatusCode.OK, 3);
        yield return MenuBurstCase("name-eq-double-quoted", [new("Name", "eq", "\"Alpha\"")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("category-eq-single-quoted", [new("Category", "eq", "'Main'")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("name-in-pipe", [new("Name", "in", "Alpha|Cola")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("name-in-comma", [new("Name", "in", "Alpha,Cola")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("name-in-quoted", [new("Name", "in", "\"Alpha\"|\"Cola\"")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("name-in-single-quoted", [new("Name", "in", "'Alpha'|'Cola'")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("name-in-quoted-with-spaces", [new("Name", "in", "  \"Alpha\"  |  \"Cola\"  ")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("category-in-single-quoted", [new("Category", "in", "'Main'")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("price-in-pipe", [new("Price", "in", "10|26|40")], HttpStatusCode.OK, 3);
        yield return MenuBurstCase("price-in-comma", [new("Price", "in", "10,26")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("price-in-pipe-with-spaces", [new("Price", "in", " 10 | 26 ")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("updatedat-in-with-null", [new("UpdatedAt", "in", "{updatedAtIso}|null")], HttpStatusCode.OK, 3);
        yield return MenuBurstCase("updatedat-in-single", [new("UpdatedAt", "in", "{updatedAtIso}")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("price-between-dotdot", [new("Price", "between", "10..26")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("price-between-pipe", [new("Price", "between", "10|26")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("price-between-comma", [new("Price", "between", "10,26")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("price-between-comma-with-spaces", [new("Price", "between", " 10 , 26 ")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("updatedat-between-dotdot", [new("UpdatedAt", "between", "{updatedAtIso}..{updatedAtIso}")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("createdat-between-dotdot", [new("CreatedAt", "between", "2000-01-01T00:00:00Z..2100-01-01T00:00:00Z")], HttpStatusCode.OK, 3);
        yield return MenuBurstCase("createdat-between-pipe", [new("CreatedAt", "between", "2000-01-01T00:00:00Z|2100-01-01T00:00:00Z")], HttpStatusCode.OK, 3);

        yield return MenuBurstCase("combined-main-price-gt20", [new("Category", "eq", "Main"), new("Price", "gt", "20")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("combined-main-updatedat-notnull", [new("Category", "eq", "Main"), new("UpdatedAt", "notnull", null)], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("combined-name-endswith-a-price-gte26", [new("Name", "endswith", "a"), new("Price", "gte", "26")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("combined-drinks-deleted", [new("Category", "eq", "Drinks"), new("IsDeleted", "eq", "true")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("combined-id-and-name", [new("Id", "eq", "{alphaId}"), new("Name", "eq", "Alpha")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("combined-updatedat-isnull-main", [new("UpdatedAt", "isnull", null), new("Category", "eq", "Main")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("combined-updatedat-notnull-main", [new("UpdatedAt", "notnull", null), new("Category", "eq", "Main")], HttpStatusCode.OK, 1);
        yield return MenuBurstCase("combined-price-between-and-main", [new("Price", "between", "10..26"), new("Category", "eq", "Main")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("combined-price-in-and-name-endswith-a", [new("Price", "in", "10|26"), new("Name", "endswith", "a")], HttpStatusCode.OK, 2);
        yield return MenuBurstCase("combined-main-price-gt-high", [new("Category", "eq", "Main"), new("Price", "gt", "1000")], HttpStatusCode.OK, 0);

        yield return MenuBurstCase("invalid-empty-property", [new("", "eq", "x")], HttpStatusCode.BadRequest, null, "Property");
        yield return MenuBurstCase("invalid-empty-operator", [new("Name", "", "x")], HttpStatusCode.BadRequest, null, "Operator");
        yield return MenuBurstCase("invalid-unknown-property", [new("Unknown", "eq", "x")], HttpStatusCode.BadRequest, null, "Invalid filter");
        yield return MenuBurstCase("invalid-price-isnull-with-value", [new("Price", "isnull", "x")], HttpStatusCode.BadRequest, null, "Invalid filter for 'Price'");
        yield return MenuBurstCase("invalid-price-between-single", [new("Price", "between", "10")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Price'");
        yield return MenuBurstCase("invalid-price-between-whitespace", [new("Price", "between", " ")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Price'");
        yield return MenuBurstCase("invalid-price-between-bad", [new("Price", "between", "bad..20")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Price'");
        yield return MenuBurstCase("invalid-price-in-bad", [new("Price", "in", "bad|20")], HttpStatusCode.BadRequest, null, "could not be converted");
        yield return MenuBurstCase("invalid-price-in-null", [new("Price", "in", "10|null")], HttpStatusCode.BadRequest, null, "does not support NULL");
        yield return MenuBurstCase("invalid-price-eq-null-token", [new("Price", "eq", "null")], HttpStatusCode.BadRequest, null, "cannot use NULL");
        yield return MenuBurstCase("invalid-id-gt", [new("Id", "gt", "{alphaId}")], HttpStatusCode.BadRequest, null, "Unsupported operator");
        yield return MenuBurstCase("invalid-name-gt", [new("Name", "gt", "Alpha")], HttpStatusCode.BadRequest, null, "Invalid filter");
        yield return MenuBurstCase("invalid-isdeleted-gt", [new("IsDeleted", "gt", "false")], HttpStatusCode.BadRequest, null, "Unsupported operator");
        yield return MenuBurstCase("invalid-updatedat-gt-null", [new("UpdatedAt", "gt", null)], HttpStatusCode.BadRequest, null, "Invalid filter");
        yield return MenuBurstCase("invalid-name-any", [new("Name", "any", "Alpha")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Name'");
        yield return MenuBurstCase("invalid-category-between-string", [new("Category", "between", "A..Z")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Category'");
        yield return MenuBurstCase("invalid-price-contains", [new("Price", "contains", "1")], HttpStatusCode.BadRequest, null, "Unsupported operator");
        yield return MenuBurstCase("invalid-createdat-bad-date", [new("CreatedAt", "eq", "not-a-date")], HttpStatusCode.BadRequest, null, "Invalid filter");
        yield return MenuBurstCase("invalid-ordering-like-filter", [new("Name", "orderby", "x")], HttpStatusCode.BadRequest, null, "Invalid filter");
        yield return MenuBurstCase("invalid-double-filter-second-bad", [new("Category", "eq", "Main"), new("Price", "gt", "bad")], HttpStatusCode.BadRequest, null, "Invalid filter");
    }

    public static IEnumerable<object[]> OrderBulkBurstCases()
    {
        var eqOps = new[] { "eq", "==", "=" };
        var neqOps = new[] { "neq", "!=", "<>" };
        var gtOps = new[] { "gt", ">" };
        var gteOps = new[] { "gte", ">=" };
        var ltOps = new[] { "lt", "<" };
        var lteOps = new[] { "lte", "<=" };

        foreach (var op in eqOps)
        {
            yield return OrderBurstCase($"customer-two-{OperatorToken(op)}", [new("CustomerEmail", op, "two@local.test")], HttpStatusCode.OK, 1);
            yield return OrderBurstCase($"status-paid-{OperatorToken(op)}", [new("Status", op, "Paid")], HttpStatusCode.OK, 3);
            yield return OrderBurstCase($"businessdate-order1-{OperatorToken(op)}", [new("BusinessDate", op, "{order1BusinessDate}")], HttpStatusCode.OK, 1);
            yield return OrderBurstCase($"businesstime-order2-{OperatorToken(op)}", [new("BusinessTime", op, "{order2BusinessTime}")], HttpStatusCode.OK, 1);
            yield return OrderBurstCase($"window-10m-{OperatorToken(op)}", [new("FulfillmentWindow", op, "00:10:00")], HttpStatusCode.OK, 1);
            yield return OrderBurstCase($"id-order1-{OperatorToken(op)}", [new("Id", op, "{order1Id}")], HttpStatusCode.OK, 1);
        }

        foreach (var op in neqOps)
        {
            yield return OrderBurstCase($"customer-not-two-{OperatorToken(op)}", [new("CustomerEmail", op, "two@local.test")], HttpStatusCode.OK, 2);
            yield return OrderBurstCase($"status-not-failed-{OperatorToken(op)}", [new("Status", op, "Failed")], HttpStatusCode.OK, 3);
            yield return OrderBurstCase($"window-not-10m-{OperatorToken(op)}", [new("FulfillmentWindow", op, "00:10:00")], HttpStatusCode.OK, 2);
            yield return OrderBurstCase($"id-not-order1-{OperatorToken(op)}", [new("Id", op, "{order1Id}")], HttpStatusCode.OK, 2);
        }

        foreach (var op in gtOps)
        {
            yield return OrderBurstCase($"total-gt20-{OperatorToken(op)}", [new("Total", op, "20")], HttpStatusCode.OK, 2);
        }

        foreach (var op in gteOps)
        {
            yield return OrderBurstCase($"total-gte40-{OperatorToken(op)}", [new("Total", op, "40")], HttpStatusCode.OK, 2);
        }

        foreach (var op in ltOps)
        {
            yield return OrderBurstCase($"total-lt20-{OperatorToken(op)}", [new("Total", op, "20")], HttpStatusCode.OK, 1);
        }

        foreach (var op in lteOps)
        {
            yield return OrderBurstCase($"total-lte10-{OperatorToken(op)}", [new("Total", op, "10")], HttpStatusCode.OK, 1);
        }

        yield return OrderBurstCase("total-between-dotdot", [new("Total", "between", "10..40")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("total-between-pipe", [new("Total", "between", "10|40")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("total-in-pipe", [new("Total", "in", "10|40|78")], HttpStatusCode.OK, 3);
        yield return OrderBurstCase("total-in-comma", [new("Total", "in", "10,78")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("id-in-order1-order3", [new("Id", "in", "{order1Id}|{order3Id}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("status-in-paid-failed", [new("Status", "in", "paid|FAILED")], HttpStatusCode.OK, 3);
        yield return OrderBurstCase("customer-in-two-three", [new("CustomerEmail", "in", "two@local.test|three@local.test")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("businessdate-in", [new("BusinessDate", "in", "{order1BusinessDate}|{order3BusinessDate}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("businessdate-in-comma", [new("BusinessDate", "in", "{order1BusinessDate},{order3BusinessDate}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("businessdate-between-dotdot", [new("BusinessDate", "between", "{businessDateMin}..{businessDateMax}")], HttpStatusCode.OK, 3);
        yield return OrderBurstCase("businessdate-between-comma", [new("BusinessDate", "between", "{businessDateMin},{businessDateMax}")], HttpStatusCode.OK, 3);
        yield return OrderBurstCase("businesstime-in", [new("BusinessTime", "in", "{order1BusinessTime}|{order3BusinessTime}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("businesstime-between-dotdot", [new("BusinessTime", "between", "{businessTimeMin}..{businessTimeMid}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("businesstime-between-pipe", [new("BusinessTime", "between", "{businessTimeMin}|{businessTimeMax}")], HttpStatusCode.OK, 3);
        yield return OrderBurstCase("fulfillmentwindow-between-dotdot", [new("FulfillmentWindow", "between", "{fulfillmentMin}..{fulfillmentMid}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("fulfillmentwindow-in-pipe", [new("FulfillmentWindow", "in", "{fulfillmentMin}|{fulfillmentMid}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("fulfillmentwindow-in-comma", [new("FulfillmentWindow", "in", "{fulfillmentMin},{fulfillmentMid}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("paymentid-notnull", [new("PaymentId", "notnull", null)], HttpStatusCode.OK, 3);
        yield return OrderBurstCase("paymentid-isnull", [new("PaymentId", "isnull", null)], HttpStatusCode.OK, 0);
        yield return OrderBurstCase("paymentid-eq-order1", [new("PaymentId", "eq", "{order1PaymentId}")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("paymentid-neq-order1", [new("PaymentId", "neq", "{order1PaymentId}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("paymentid-eq-null", [new("PaymentId", "eq", null)], HttpStatusCode.OK, 0);
        yield return OrderBurstCase("paymentid-neq-null", [new("PaymentId", "neq", null)], HttpStatusCode.OK, 3);
        yield return OrderBurstCase("paymentid-in-order1-order3", [new("PaymentId", "in", "{order1PaymentId}|{order3PaymentId}")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("paymentids-any-id1", [new("PaymentIds", "any", "{order1PaymentId}")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("paymentarray-any-id2", [new("PaymentArrayIds", "any", "{order2PaymentId}")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("paymentset-any-id3", [new("PaymentSetIds", "any", "{order3PaymentId}")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("tags-any-highqty1", [new("Tags", "any", "HighQty1")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("tags-any-highqty1-lower", [new("Tags", "any", "highqty1")], HttpStatusCode.OK, 0);
        yield return OrderBurstCase("lines-any-qty-gt2", [new("Lines", "any", "Quantity>2")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("lines-all-qty-gte1", [new("Lines", "all", "Quantity>=1")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("lines-all-qty-gte2", [new("Lines", "all", "Quantity>=2")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("lines-any-qty-between-dotdot", [new("Lines", "any", "Quantity between 2..3")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("lines-any-qty-between-comma", [new("Lines", "any", "Quantity between 2,3")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Lines'");
        yield return OrderBurstCase("lines-any-qty-in", [new("Lines", "any", "Quantity in 1|4")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Lines'");
        yield return OrderBurstCase("lines-any-date-eq", [new("Lines", "any", "ScheduledDate==2024-01-02")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("lines-any-time-gte", [new("Lines", "any", "ScheduledTime>=10:00")], HttpStatusCode.OK, 2);
        yield return OrderBurstCase("lines-any-date-between-dotdot", [new("Lines", "any", "ScheduledDate between 2024-01-02..2024-01-03")], HttpStatusCode.OK, 1);
        yield return OrderBurstCase("lines-any-date-between-comma", [new("Lines", "any", "ScheduledDate between 2024-01-02,2024-01-03")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Lines'");
        yield return OrderBurstCase("lines-all-date-gte", [new("Lines", "all", "ScheduledDate>=2024-01-01")], HttpStatusCode.OK, 1);

        yield return OrderBurstCase("invalid-status-gt", [new("Status", "gt", "Paid")], HttpStatusCode.BadRequest, null, "Unsupported operator");
        yield return OrderBurstCase("invalid-window-gt", [new("FulfillmentWindow", "gt", "00:10:00")], HttpStatusCode.BadRequest, null, "Invalid filter");
        yield return OrderBurstCase("invalid-businessdate", [new("BusinessDate", "eq", "bad-date")], HttpStatusCode.BadRequest, null, "Invalid filter");
        yield return OrderBurstCase("invalid-businesstime", [new("BusinessTime", "eq", "bad-time")], HttpStatusCode.BadRequest, null, "Invalid filter");
        yield return OrderBurstCase("invalid-paymentarray-any-guid", [new("PaymentArrayIds", "any", "not-a-guid")], HttpStatusCode.BadRequest, null, "Invalid filter: property='PaymentArrayIds'");
        yield return OrderBurstCase("invalid-paymentids-any-nested", [new("PaymentIds", "any", "Quantity>1")], HttpStatusCode.BadRequest, null, "Invalid filter: property='PaymentIds'");
        yield return OrderBurstCase("invalid-customer-any", [new("CustomerEmail", "any", "admin")], HttpStatusCode.BadRequest, null, "Invalid filter: property='CustomerEmail'");
        yield return OrderBurstCase("invalid-lines-any-list", [new("Lines", "any", "first,second")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Lines'");
        yield return OrderBurstCase("invalid-lines-any-isnull", [new("Lines", "any", "Quantity isnull")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Lines'");
        yield return OrderBurstCase("invalid-lines-all-missing-value", [new("Lines", "all", "Quantity>")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Lines'");
        yield return OrderBurstCase("invalid-tags-all-list", [new("Tags", "all", "HighQty1,HighQty2")], HttpStatusCode.BadRequest, null, "Invalid filter: property='Tags'");
        yield return OrderBurstCase("invalid-unknown-property", [new("Unknown", "eq", "x")], HttpStatusCode.BadRequest, null, "Invalid filter");
    }

    private static async Task SeedMenuItemsAsync(HttpClient client)
    {
        await CreateMenuItemAsync(client, "Alpha", "Main", 10);
        var updated = await CreateMenuItemAsync(client, "Beta", "Main", 25);
        await CreateMenuItemAsync(client, "Cola", "Drinks", 40);

        updated.Price = 26;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{updated.Id}", updated);
        updateResponse.EnsureSuccessStatusCode();
    }

    private static async Task<MenuItem> CreateMenuItemAsync(HttpClient client, string name, string category, decimal price, bool isDeleted = false)
    {
        var response = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = name,
            Category = category,
            Price = price,
            IsDeleted = isDeleted
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        var item = await response.Content.ReadFromJsonAsync<MenuItem>();
        item.ShouldNotBeNull();
        return item!;
    }

    private static async Task<Order> CreateOrderAsync(HttpClient client, string email, List<OrderLine> lines)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(email, "card", lines));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var order = await response.Content.ReadFromJsonAsync<Order>();
        order.ShouldNotBeNull();
        return order!;
    }

    private static async Task SeedOrdersForAnyAllMatrixAsync(HttpClient client)
    {
        await CreateOrderAsync(
            client,
            "one@local.test",
            [new OrderLine { MenuItemId = Guid.NewGuid(), Name = "LowQty", UnitPrice = 10, Quantity = 1 }]);

        await CreateOrderAsync(
            client,
            "two@local.test",
            [
                new OrderLine { MenuItemId = Guid.NewGuid(), Name = "HighQty1", UnitPrice = 10, Quantity = 3 },
                new OrderLine { MenuItemId = Guid.NewGuid(), Name = "HighQty2", UnitPrice = 12, Quantity = 4 }
            ]);

        await CreateOrderAsync(
            client,
            "three@local.test",
            [new OrderLine { MenuItemId = Guid.NewGuid(), Name = "MidQty", UnitPrice = 20, Quantity = 2 }]);
    }

    private static object[] MenuBurstCase(
        string caseName,
        QueryHelperFilter[] filters,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment = null)
        => [caseName, filters, expectedStatus, expectedCount, expectedFragment];

    private static object[] OrderBurstCase(
        string caseName,
        QueryHelperFilter[] filters,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment = null)
        => [caseName, filters, expectedStatus, expectedCount, expectedFragment];

    private static QueryHelperFilter[] ReplaceMenuTokens(QueryHelperFilter[] filters, MenuBurstSeedContext seed)
    {
        return filters
            .Select(f => new QueryHelperFilter(f.Property, f.Operator, ReplaceMenuTokens(f.Value, seed)))
            .ToArray();
    }

    private static QueryHelperFilter[] ReplaceOrderTokens(QueryHelperFilter[] filters, OrderBurstSeedContext seed)
    {
        return filters
            .Select(f => new QueryHelperFilter(f.Property, f.Operator, ReplaceOrderTokens(f.Value, seed)))
            .ToArray();
    }

    private static string? ReplaceMenuTokens(string? value, MenuBurstSeedContext seed)
    {
        if (value is null)
        {
            return null;
        }

        return value
            .Replace("{alphaId}", seed.AlphaId.ToString(), StringComparison.Ordinal)
            .Replace("{updatedAtIso}", seed.UpdatedAtIso, StringComparison.Ordinal);
    }

    private static string? ReplaceOrderTokens(string? value, OrderBurstSeedContext seed)
    {
        if (value is null)
        {
            return null;
        }

        return value
            .Replace("{order1Id}", seed.Order1Id.ToString(), StringComparison.Ordinal)
            .Replace("{order3Id}", seed.Order3Id.ToString(), StringComparison.Ordinal)
            .Replace("{order1PaymentId}", seed.Order1PaymentId.ToString(), StringComparison.Ordinal)
            .Replace("{order2PaymentId}", seed.Order2PaymentId.ToString(), StringComparison.Ordinal)
            .Replace("{order3PaymentId}", seed.Order3PaymentId.ToString(), StringComparison.Ordinal)
            .Replace("{order1BusinessDate}", seed.Order1BusinessDate, StringComparison.Ordinal)
            .Replace("{order3BusinessDate}", seed.Order3BusinessDate, StringComparison.Ordinal)
            .Replace("{order2BusinessTime}", seed.Order2BusinessTime, StringComparison.Ordinal)
            .Replace("{order1BusinessTime}", seed.Order1BusinessTime, StringComparison.Ordinal)
            .Replace("{order3BusinessTime}", seed.Order3BusinessTime, StringComparison.Ordinal)
            .Replace("{businessTimeMin}", seed.BusinessTimeMin, StringComparison.Ordinal)
            .Replace("{businessTimeMid}", seed.BusinessTimeMid, StringComparison.Ordinal)
            .Replace("{businessTimeMax}", seed.BusinessTimeMax, StringComparison.Ordinal)
            .Replace("{fulfillmentMin}", seed.FulfillmentMin, StringComparison.Ordinal)
            .Replace("{fulfillmentMid}", seed.FulfillmentMid, StringComparison.Ordinal)
            .Replace("{fulfillmentMax}", seed.FulfillmentMax, StringComparison.Ordinal)
            .Replace("{businessDateMin}", seed.BusinessDateMin, StringComparison.Ordinal)
            .Replace("{businessDateMax}", seed.BusinessDateMax, StringComparison.Ordinal);
    }

    private static async Task<MenuBurstSeedContext> SeedMenuItemsForBurstMatrixAsync(HttpClient client)
    {
        var alpha = await CreateMenuItemAsync(client, "Alpha", "Main", 10, isDeleted: false);
        var beta = await CreateMenuItemAsync(client, "Beta", "Main", 25, isDeleted: false);
        await CreateMenuItemAsync(client, "Cola", "Drinks", 40, isDeleted: true);

        beta.Price = 26;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{beta.Id}", beta);
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, updateBody);
        var updated = await updateResponse.Content.ReadFromJsonAsync<MenuItem>();
        updated.ShouldNotBeNull();
        updated!.UpdatedAt.ShouldNotBeNull();

        return new MenuBurstSeedContext(
            AlphaId: alpha.Id,
            UpdatedAtIso: updated.UpdatedAt!.Value.ToString("o"));
    }

    private static async Task<OrderBurstSeedContext> SeedOrdersForBurstMatrixAsync(HttpClient client)
    {
        var first = await CreateOrderAsync(
            client,
            "one@local.test",
            [
                new OrderLine
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "LowQty",
                    UnitPrice = 10,
                    Quantity = 1,
                    ScheduledDate = new DateOnly(2024, 1, 1),
                    ScheduledTime = new TimeOnly(8, 0),
                    PrepDuration = TimeSpan.FromMinutes(5)
                }
            ]);

        var second = await CreateOrderAsync(
            client,
            "two@local.test",
            [
                new OrderLine
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "HighQty1",
                    UnitPrice = 10,
                    Quantity = 3,
                    ScheduledDate = new DateOnly(2024, 1, 2),
                    ScheduledTime = new TimeOnly(9, 15),
                    PrepDuration = TimeSpan.FromMinutes(15)
                },
                new OrderLine
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "HighQty2",
                    UnitPrice = 12,
                    Quantity = 4,
                    ScheduledDate = new DateOnly(2024, 1, 3),
                    ScheduledTime = new TimeOnly(10, 30),
                    PrepDuration = TimeSpan.FromMinutes(20)
                }
            ]);

        var third = await CreateOrderAsync(
            client,
            "three@local.test",
            [
                new OrderLine
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "MidQty",
                    UnitPrice = 20,
                    Quantity = 2,
                    ScheduledDate = new DateOnly(2024, 1, 4),
                    ScheduledTime = new TimeOnly(11, 45),
                    PrepDuration = TimeSpan.FromMinutes(10)
                }
            ]);

        first.PaymentId.ShouldNotBeNull();
        second.PaymentId.ShouldNotBeNull();
        third.PaymentId.ShouldNotBeNull();

        var businessDates = new[] { first.BusinessDate, second.BusinessDate, third.BusinessDate }
            .OrderBy(x => x)
            .ToArray();
        var businessTimes = new[] { first.BusinessTime, second.BusinessTime, third.BusinessTime }
            .OrderBy(x => x)
            .ToArray();
        var fulfillmentWindows = new[] { first.FulfillmentWindow, second.FulfillmentWindow, third.FulfillmentWindow }
            .OrderBy(x => x)
            .ToArray();

        return new OrderBurstSeedContext(
            Order1Id: first.Id,
            Order3Id: third.Id,
            Order1PaymentId: first.PaymentId!.Value,
            Order2PaymentId: second.PaymentId!.Value,
            Order3PaymentId: third.PaymentId!.Value,
            Order1BusinessDate: first.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Order3BusinessDate: third.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Order1BusinessTime: first.BusinessTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Order2BusinessTime: second.BusinessTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Order3BusinessTime: third.BusinessTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            BusinessTimeMin: businessTimes[0].ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            BusinessTimeMid: businessTimes[1].ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            BusinessTimeMax: businessTimes[^1].ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            FulfillmentMin: fulfillmentWindows[0].ToString("c", CultureInfo.InvariantCulture),
            FulfillmentMid: fulfillmentWindows[1].ToString("c", CultureInfo.InvariantCulture),
            FulfillmentMax: fulfillmentWindows[^1].ToString("c", CultureInfo.InvariantCulture),
            BusinessDateMin: businessDates[0].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            BusinessDateMax: businessDates[^1].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static string OperatorToken(string op)
    {
        return op switch
        {
            "==" => "eqeq",
            "!=" => "noteq",
            "<>" => "angle-noteq",
            ">=" => "gte",
            "<=" => "lte",
            ">" => "gt",
            "<" => "lt",
            _ => op
        };
    }

    private static string NormalizeCaseName(string caseName)
    {
        var chars = caseName
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
            .ToArray();
        var normalized = new string(chars);
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }
        return normalized.Trim('-');
    }

    private sealed record MenuBurstSeedContext(Guid AlphaId, string UpdatedAtIso);

    private sealed record OrderBurstSeedContext(
        Guid Order1Id,
        Guid Order3Id,
        Guid Order1PaymentId,
        Guid Order2PaymentId,
        Guid Order3PaymentId,
        string Order1BusinessDate,
        string Order3BusinessDate,
        string Order1BusinessTime,
        string Order2BusinessTime,
        string Order3BusinessTime,
        string BusinessTimeMin,
        string BusinessTimeMid,
        string BusinessTimeMax,
        string FulfillmentMin,
        string FulfillmentMid,
        string FulfillmentMax,
        string BusinessDateMin,
        string BusinessDateMax);

    public sealed record QueryHelperRequest(
        QueryHelperFilter[]? Filters = null,
        QueryHelperOrder[]? OrderBy = null,
        bool? AsNoTracking = null,
        bool? UseSplitQuery = null);

    public sealed record QueryHelperFilter(
        string Property,
        [property: JsonPropertyName("operator")] string Operator,
        string? Value);

    public sealed record QueryHelperOrder(string Property, bool Desc = false);
}
