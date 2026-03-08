using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class FilterBuilderDiagnosticsIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Theory(DisplayName = "Marten filter builder - string mode scenarios")]
    [MemberData(nameof(StringModeCases))]
    public async Task Filter_builder_string_mode_scenarios(
        FilterBuilderRequest request,
        HttpStatusCode expectedStatus,
        int? expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-filter-builder-menu-string"));
        await SeedMenuItemsAsync(client);

        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/filter-builder", request);
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

    [Theory(DisplayName = "Marten filter builder - clauses mode scenarios")]
    [MemberData(nameof(ClausesModeCases))]
    public async Task Filter_builder_clauses_mode_scenarios(
        FilterBuilderRequest request,
        HttpStatusCode expectedStatus,
        int? expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-filter-builder-menu-clauses"));
        await SeedMenuItemsAsync(client);

        var response = await client.PostAsJsonAsync("/api/menu-items/diagnostics/filter-builder", request);
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

    [Theory(DisplayName = "Marten filter builder - any/all scenarios")]
    [MemberData(nameof(AnyAllFilterCases))]
    public async Task Filter_builder_any_all_scenarios(
        FilterBuilderRequest request,
        HttpStatusCode expectedStatus,
        int? expectedCount)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId("marten-filter-builder-orders"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

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

        var response = await client.PostAsJsonAsync("/api/orders/diagnostics/filter-builder", request);
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

    public static IEnumerable<object[]> StringModeCases()
    {
        yield return [new FilterBuilderRequest(Filter: "Name==\"alpha\"", CaseInsensitive: true), HttpStatusCode.OK, 1];
        yield return [new FilterBuilderRequest(Filter: "Name in [alpha,beta]", CaseInsensitive: true), HttpStatusCode.OK, 2];
        yield return [new FilterBuilderRequest(Filter: "UpdatedAt==null"), HttpStatusCode.OK, 2];
        yield return [new FilterBuilderRequest(Filter: "Price>10", AllowedProperties: ["Name"], Strict: false), HttpStatusCode.OK, 3];
        yield return [new FilterBuilderRequest(Filter: "Price>10", AllowedProperties: ["Name"], Strict: true), HttpStatusCode.BadRequest, (int?)null];
        yield return [new FilterBuilderRequest(Filter: "Name in [Alpha,Beta"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new FilterBuilderRequest(Filter: "Name==\"Alpha"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new FilterBuilderRequest(Filter: "Price between 10"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new FilterBuilderRequest(Filter: "Price contains 1"), HttpStatusCode.BadRequest, (int?)null];
    }

    public static IEnumerable<object[]> ClausesModeCases()
    {
        yield return
        [
            new FilterBuilderRequest(
                Clauses:
                [
                    new FilterClausePayload("Category", "eq", "Main"),
                    new FilterClausePayload("Price", "gte", "20")
                ]),
            HttpStatusCode.OK,
            1
        ];
        yield return
        [
            new FilterBuilderRequest(
                Clauses:
                [
                    new FilterClausePayload("UpdatedAt", "neq", null)
                ]),
            HttpStatusCode.OK,
            1
        ];
        yield return
        [
            new FilterBuilderRequest(
                Clauses:
                [
                    new FilterClausePayload("Price", "gt", "10")
                ],
                AllowedProperties: ["Name"],
                Strict: false),
            HttpStatusCode.OK,
            3
        ];
        yield return
        [
            new FilterBuilderRequest(
                Clauses:
                [
                    new FilterClausePayload("Price", "gt", "10")
                ],
                AllowedProperties: ["Name"],
                Strict: true),
            HttpStatusCode.BadRequest,
            (int?)null
        ];
        yield return
        [
            new FilterBuilderRequest(
                Clauses:
                [
                    new FilterClausePayload("Price", "eq", "null")
                ]),
            HttpStatusCode.BadRequest,
            (int?)null
        ];
        yield return
        [
            new FilterBuilderRequest(
                Clauses:
                [
                    new FilterClausePayload("Price", "has", "10")
                ]),
            HttpStatusCode.BadRequest,
            (int?)null
        ];
        yield return
        [
            new FilterBuilderRequest(
                Clauses:
                [
                    new FilterClausePayload("Unknown", "eq", "1")
                ]),
            HttpStatusCode.BadRequest,
            (int?)null
        ];
    }

    public static IEnumerable<object[]> AnyAllFilterCases()
    {
        yield return [new FilterBuilderRequest(Filter: "CustomerEmail==\"two@local.test\""), HttpStatusCode.OK, 1];
        yield return [new FilterBuilderRequest(Filter: "Lines any Quantity>1"), HttpStatusCode.OK, 1];
        yield return [new FilterBuilderRequest(Filter: "Lines all Quantity>1"), HttpStatusCode.OK, 0];
        yield return [new FilterBuilderRequest(Filter: "CustomerEmail any admin"), HttpStatusCode.BadRequest, (int?)null];
        yield return [new FilterBuilderRequest(Filter: "Lines any first,second"), HttpStatusCode.BadRequest, (int?)null];
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

    public sealed record FilterBuilderRequest(
        string? Filter = null,
        FilterClausePayload[]? Clauses = null,
        string[]? AllowedProperties = null,
        bool? Strict = null,
        bool? CaseInsensitive = null);

    public sealed record FilterClausePayload(string Property, string Operator, string? Value);
}
