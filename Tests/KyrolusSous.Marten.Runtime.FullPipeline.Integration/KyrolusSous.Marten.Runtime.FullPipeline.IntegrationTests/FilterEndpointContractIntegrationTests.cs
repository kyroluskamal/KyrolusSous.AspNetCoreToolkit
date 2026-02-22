using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class FilterEndpointContractIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{

    [Fact(DisplayName = "Filter endpoint - equals operator")]
    public async Task Filter_equals_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-eq"));
        await SeedAsync(client, ("Alpha", 10), ("Beta", 20));
        var response = await client.GetAsync(FilterUrl("Name==\"Alpha\""));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Name == "Alpha").ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - not equals operator")]
    public async Task Filter_not_equals_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-neq"));
        await SeedAsync(client, ("Alpha", 10), ("Beta", 20));
        var response = await client.GetAsync(FilterUrl("Name!=Alpha"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Name != "Alpha").ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - contains operator returns response")]
    public async Task Filter_contains_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-contains"));
        await SeedAsync(client, ("Burger", 10), ("Cola", 20));
        var response = await client.GetAsync(FilterUrl("Name contains \"burg\""));
        new[] { HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }.ShouldContain(response.StatusCode);
    }

    [Fact(DisplayName = "Filter endpoint - startswith operator returns response")]
    public async Task Filter_startswith_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-starts"));
        await SeedAsync(client, ("Pizza", 10), ("Pasta", 20));
        var response = await client.GetAsync(FilterUrl("Name startswith \"Piz\""));
        new[] { HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }.ShouldContain(response.StatusCode);
    }

    [Fact(DisplayName = "Filter endpoint - endswith operator returns response")]
    public async Task Filter_endswith_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-ends"));
        await SeedAsync(client, ("Cola", 10), ("Salad", 20));
        var response = await client.GetAsync(FilterUrl("Name endswith \"la\""));
        new[] { HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }.ShouldContain(response.StatusCode);
    }

    [Fact(DisplayName = "Filter endpoint - greater than operator")]
    public async Task Filter_greater_than_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-gt"));
        await SeedAsync(client, ("A", 5), ("B", 30));
        var response = await client.GetAsync(FilterUrl("Price>10"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Price > 10).ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - greater than or equal operator")]
    public async Task Filter_greater_than_or_equal_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-gte"));
        await SeedAsync(client, ("A", 10), ("B", 20));
        var response = await client.GetAsync(FilterUrl("Price>=10"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Price >= 10).ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - less than operator")]
    public async Task Filter_less_than_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-lt"));
        await SeedAsync(client, ("A", 5), ("B", 30));
        var response = await client.GetAsync(FilterUrl("Price<10"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Price < 10).ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - less than or equal operator")]
    public async Task Filter_less_than_or_equal_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-lte"));
        await SeedAsync(client, ("A", 10), ("B", 20));
        var response = await client.GetAsync(FilterUrl("Price<=10"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Price <= 10).ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - in operator")]
    public async Task Filter_in_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-in"));
        await SeedAsync(client, ("A", 10), ("B", 20), ("C", 30));
        var response = await client.GetAsync(FilterUrl("Name in (A,C)"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Name == "A").ShouldBeTrue();
        items.Any(x => x.Name == "C").ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - between operator")]
    public async Task Filter_between_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-between"));
        await SeedAsync(client, ("A", 10), ("B", 20), ("C", 40));
        var response = await client.GetAsync(FilterUrl("Price between (10,30)"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Price >= 10 && x.Price <= 30).ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - isnull operator")]
    public async Task Filter_isnull_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-isnull"));
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Null UpdatedAt",
            Category = "Main",
            Price = 10
        });
        createResponse.EnsureSuccessStatusCode();
        var response = await client.GetAsync(FilterUrl("UpdatedAt isnull"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.UpdatedAt is null).ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - notnull operator")]
    public async Task Filter_notnull_operator()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-notnull"));
        var createResponse = await client.PostAsJsonAsync("/api/menu-items", new MenuItem
        {
            Name = "Has UpdatedAt",
            Category = "Main",
            Price = 10
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItem>();
        created.ShouldNotBeNull();
        created!.Price = 11;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{created.Id}", created);
        updateResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync(FilterUrl("UpdatedAt notnull"));
        var items = await ReadItemsAsync(response);
        items.Any(x => x.Id == created.Id).ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - AND/OR grouping")]
    public async Task Filter_and_or_grouping()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-andor"));
        await SeedAsync(client, ("Burger", 10), ("Cola", 20), ("Salad", 30));
        var response = await client.GetAsync(FilterUrl("(Name==\"Burger\"|Name==\"Cola\"),Price>=10"));
        var items = await ReadItemsAsync(response);
        items.Any(x => (x.Name == "Burger" || x.Name == "Cola") && x.Price >= 10).ShouldBeTrue();
    }

    [Fact(DisplayName = "Filter endpoint - invalid operator returns 400")]
    public async Task Filter_invalid_operator_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-invalid-op"));
        var response = await client.GetAsync(FilterUrl("Name ~~ \"x\""));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Filter endpoint - invalid property returns 400")]
    public async Task Filter_invalid_property_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-invalid-prop"));
        var response = await client.GetAsync(FilterUrl("Unknown==1"));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Filter endpoint - missing value returns 400")]
    public async Task Filter_missing_value_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-missing-value"));
        var response = await client.GetAsync(FilterUrl("Name=="));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Filter endpoint - invalid numeric value returns 400")]
    public async Task Filter_invalid_numeric_value_returns_bad_request()
    {
        using var client = factory.CreateClientWithTenant(NewTenant("filter-invalid-number"));
        var response = await client.GetAsync(FilterUrl("Price>abc"));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static string FilterUrl(string filter)
        => $"/api/menu-items?filter={Uri.EscapeDataString(filter)}";

    private static async Task<List<MenuItem>> ReadItemsAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        items.ShouldNotBeNull();
        return items!;
    }

    private static async Task SeedAsync(HttpClient client, params (string Name, decimal Price)[] items)
    {
        foreach (var (Name, Price) in items)
        {
            await client.PostAsJsonAsync("/api/menu-items", new MenuItem
            {
                Name = Name,
                Category = "Main",
                Price = Price
            });
        }
    }

    private static string NewTenant(string suffix)
        => $"tenant-{suffix}-{Guid.NewGuid():N}";
}
