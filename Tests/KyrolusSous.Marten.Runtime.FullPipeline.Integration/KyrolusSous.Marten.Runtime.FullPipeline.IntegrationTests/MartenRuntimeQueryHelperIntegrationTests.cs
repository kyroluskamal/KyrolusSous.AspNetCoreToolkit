using System.Net;
using System.Net.Http.Json;
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

    private static async Task SeedMenuItemsAsync(HttpClient client)
    {
        await CreateMenuItemAsync(client, "Alpha", "Main", 10);
        var updated = await CreateMenuItemAsync(client, "Beta", "Main", 25);
        await CreateMenuItemAsync(client, "Cola", "Drinks", 40);

        updated.Price = 26;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{updated.Id}", updated);
        updateResponse.EnsureSuccessStatusCode();
    }

    private static async Task<MenuItem> CreateMenuItemAsync(HttpClient client, string name, string category, decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = name,
            Category = category,
            Price = price
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
