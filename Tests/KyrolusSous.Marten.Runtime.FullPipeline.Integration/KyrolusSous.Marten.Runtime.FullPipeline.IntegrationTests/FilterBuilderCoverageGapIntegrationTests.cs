using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class FilterBuilderCoverageGapIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Theory(DisplayName = "Marten filter builder menu - type conversion and token parsing matrix")]
    [MemberData(nameof(MenuFilterTemplateCases))]
    public async Task Filter_builder_menu_type_conversion_and_token_parsing_matrix(
        string caseName,
        string filterTemplate,
        bool caseInsensitive,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"marten-filter-builder-menu-gap-{caseName}"));
        var seeded = await SeedMenuItemsAsync(client);
        var filter = ReplaceTokens(filterTemplate, seeded);

        var response = await client.PostAsJsonAsync(
            "/api/menu-items/diagnostics/filter-builder",
            new FilterBuilderRequest(Filter: filter, CaseInsensitive: caseInsensitive));
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

        var payload = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Count.ShouldBe(expectedCount.Value, body);
    }

    [Theory(DisplayName = "Marten filter builder orders - any all nested and enum matrix")]
    [MemberData(nameof(OrderFilterTemplateCases))]
    public async Task Filter_builder_orders_any_all_nested_and_enum_matrix(
        string caseName,
        string filterTemplate,
        bool caseInsensitive,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"marten-filter-builder-order-gap-{caseName}"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);
        var seeded = await SeedOrdersAsync(client);
        var filter = ReplaceTokens(filterTemplate, seeded);

        var response = await client.PostAsJsonAsync(
            "/api/orders/diagnostics/filter-builder",
            new FilterBuilderRequest(Filter: filter, CaseInsensitive: caseInsensitive));
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

        var payload = await response.Content.ReadFromJsonAsync<List<Order>>();
        payload.ShouldNotBeNull();
        payload!.Count.ShouldBe(expectedCount.Value, body);
    }

    [Theory(DisplayName = "Marten filter builder menu clauses - operator and nullable guard matrix")]
    [MemberData(nameof(MenuClauseCases))]
    public async Task Filter_builder_menu_clauses_operator_and_nullable_guard_matrix(
        string caseName,
        FilterClausePayload[] clauses,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"marten-filter-builder-clauses-gap-{caseName}"));
        await SeedMenuItemsAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/menu-items/diagnostics/filter-builder",
            new FilterBuilderRequest(Clauses: clauses));
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

        var payload = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        payload.ShouldNotBeNull();
        payload!.Count.ShouldBe(expectedCount.Value, body);
    }

    [Theory(DisplayName = "Marten filter builder orders clauses - collection enum and nullable matrix")]
    [MemberData(nameof(OrderClauseCases))]
    public async Task Filter_builder_orders_clauses_collection_enum_and_nullable_matrix(
        string caseName,
        FilterClausePayload[] clauses,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"marten-filter-builder-order-clauses-gap-{caseName}"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);
        var seeded = await SeedOrdersAsync(client);
        var replacedClauses = ReplaceTokens(clauses, seeded);

        var response = await client.PostAsJsonAsync(
            "/api/orders/diagnostics/filter-builder",
            new FilterBuilderRequest(Clauses: replacedClauses));
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

        var payload = await response.Content.ReadFromJsonAsync<List<Order>>();
        payload.ShouldNotBeNull();
        payload!.Count.ShouldBe(expectedCount.Value, body);
    }

    public static IEnumerable<object?[]> MenuFilterTemplateCases()
    {
        yield return ["id-eq", "Id=={alphaId}", false, HttpStatusCode.OK, 1, null];
        yield return ["id-invalid", "Id==not-a-guid", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to Guid"];
        yield return ["price-eq-symbol", "Price=10", false, HttpStatusCode.OK, 1, null];
        yield return ["price-neq-angle", "Price<>10", false, HttpStatusCode.OK, 3, null];
        yield return ["price-lt-alias", "Price lt 20", false, HttpStatusCode.OK, 2, null];
        yield return ["price-lte-alias", "Price lte 15", false, HttpStatusCode.OK, 2, null];
        yield return ["createdat-gt", "CreatedAt>2000-01-01T00:00:00Z", false, HttpStatusCode.OK, 4, null];
        yield return ["createdat-between-unspecified", "CreatedAt between [2000-01-01T00:00:00,2999-01-01T00:00:00]", false, HttpStatusCode.OK, 4, null];
        yield return ["createdat-between-offset", "CreatedAt between [2000-01-01T00:00:00+02:00,2999-01-01T00:00:00+02:00]", false, HttpStatusCode.OK, 4, null];
        yield return ["createdat-invalid", "CreatedAt==not-a-dto", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to DateTimeOffset"];
        yield return ["price-eq-null-string", "Price==null", false, HttpStatusCode.BadRequest, (int?)null, "does not allow null values."];
        yield return ["updatedat-eq-null-string", "UpdatedAt==null", false, HttpStatusCode.OK, 3, null];
        yield return ["updatedat-neq-null-string", "UpdatedAt!=null", false, HttpStatusCode.OK, 1, null];
        yield return ["updatedat-in-nullable", "UpdatedAt in [{updatedAtIso}]", false, HttpStatusCode.OK, 1, null];
        yield return ["updatedat-between-nullable", "UpdatedAt between [{updatedAtMinIso},{updatedAtMaxIso}]", false, HttpStatusCode.OK, 1, null];
        yield return ["updatedat-between-paren-nullable", "UpdatedAt between ({updatedAtMinIso},{updatedAtMaxIso})", false, HttpStatusCode.OK, 1, null];
        yield return ["updatedat-between-invalid", "UpdatedAt between [invalid,{updatedAtMaxIso}]", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to DateTimeOffset"];
        yield return ["updatedat-gt-null-string", "UpdatedAt gt null", false, HttpStatusCode.BadRequest, (int?)null, "does not allow null values."];
        yield return ["quoted-escape-eq", "Name=='Al\\'pha'", false, HttpStatusCode.OK, 1, null];
        yield return ["list-quoted-escape-in", "Name in ['Al\\'pha','Beta']", false, HttpStatusCode.OK, 2, null];
        yield return ["name-in-paren-list", "Name in (Alpha,Beta)", false, HttpStatusCode.OK, 2, null];
        yield return ["name-eq-case-insensitive", "Name=='alpha'", true, HttpStatusCode.OK, 1, null];
        yield return ["name-in-case-insensitive", "Name in [alpha,beta]", true, HttpStatusCode.OK, 2, null];
        yield return ["price-between-braces", "Price between {10,26}", false, HttpStatusCode.OK, 3, null];
        yield return ["price-between-paren", "Price between (10,26)", false, HttpStatusCode.OK, 3, null];
        yield return ["price-in-invalid-decimal", "Price in [ten,20]", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to Decimal."];
        yield return ["missing-value", "Name eq", false, HttpStatusCode.BadRequest, (int?)null, "Value is required."];
        yield return ["or-missing-right", "Name=='Alpha'|", false, HttpStatusCode.BadRequest, (int?)null, "Property name is required."];
        yield return ["and-missing-right", "Name=='Alpha',", false, HttpStatusCode.BadRequest, (int?)null, "Property name is required."];
        yield return ["missing-closing-paren", "(Name=='Alpha'", false, HttpStatusCode.BadRequest, (int?)null, "Missing closing ')'."];        
        yield return ["missing-property-expression", ",Name=='Alpha'", false, HttpStatusCode.BadRequest, (int?)null, "Property name is required."];
        yield return ["any-on-scalar-property", "Name any [Alpha]", false, HttpStatusCode.BadRequest, (int?)null, "only valid for collection properties."];
    }

    public static IEnumerable<object?[]> OrderFilterTemplateCases()
    {
        yield return ["paymentids-any-guid", "PaymentIds any [{order1PaymentId}]", false, HttpStatusCode.OK, 1, null];
        yield return ["paymentarray-any-guid", "PaymentArrayIds any [{order2PaymentId}]", false, HttpStatusCode.OK, 1, null];
        yield return ["paymentset-any-guid", "PaymentSetIds any [{order2PaymentId}]", false, HttpStatusCode.OK, 1, null];
        yield return ["paymentid-notnull", "PaymentId notnull", false, HttpStatusCode.OK, 2, null];
        yield return ["paymentid-isnull", "PaymentId isnull", false, HttpStatusCode.OK, 0, null];
        yield return ["paymentid-in-null-or-guid", "PaymentId in [null,{order1PaymentId}]", false, HttpStatusCode.OK, 1, null];
        yield return ["lines-any-nested", "Lines any Quantity>2", false, HttpStatusCode.OK, 1, null];
        yield return ["lines-all-nested", "Lines all Quantity>0", false, HttpStatusCode.OK, 0, null];
        yield return ["tags-any-case-sensitive", "Tags any [HighQty1]", false, HttpStatusCode.OK, 1, null];
        yield return ["tags-any-case-insensitive-unsupported", "Tags any [highqty1]", true, HttpStatusCode.BadRequest, (int?)null, "Case-insensitive 'any' with value lists is not supported for string collections."];
        yield return ["tags-all-list-unsupported", "Tags all [highqty1,highqty2]", true, HttpStatusCode.BadRequest, (int?)null, "Operator 'all' with value lists is not supported for Marten collections."];
        yield return ["paymentids-invalid-nested", "PaymentIds any Quantity>1", false, HttpStatusCode.BadRequest, (int?)null, "Property 'Quantity' was not found on Guid"];
        yield return ["paymentids-any-invalid-guid", "PaymentIds any [not-a-guid]", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to Guid."];
        yield return ["status-enum-success", "Status==Paid", false, HttpStatusCode.OK, 2, null];
        yield return ["status-enum-in-list", "Status in [Paid,Failed]", false, HttpStatusCode.OK, 2, null];
        yield return ["status-enum-invalid", "Status==UnknownStatus", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to OrderStatus"];
        yield return ["status-enum-invalid-in-list", "Status in [Paid,UnknownStatus]", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to OrderStatus."];
        yield return ["businessdate-eq-symbol", "BusinessDate={order1BusinessDate}", false, HttpStatusCode.OK, 1, null];
        yield return ["businessdate-neq-angle", "BusinessDate<>{order1BusinessDate}", false, HttpStatusCode.OK, 1, null];
        yield return ["businesstime-lt-alias", "BusinessTime lt {order2BusinessTime}", false, HttpStatusCode.OK, 1, null];
        yield return ["businesstime-lte-alias", "BusinessTime lte {order1BusinessTime}", false, HttpStatusCode.OK, 1, null];
        yield return ["businesstime-between-wide", "BusinessTime between [{order1BusinessTime},{order2BusinessTime}]", false, HttpStatusCode.OK, 2, null];
        yield return ["paymentid-eq-null", "PaymentId eq null", false, HttpStatusCode.OK, 0, null];
        yield return ["paymentid-gt-null", "PaymentId gt null", false, HttpStatusCode.BadRequest, (int?)null, "does not allow null values."];
        yield return ["lines-any-nested-or", "Lines any (Quantity>2|Name=='LowQty')", false, HttpStatusCode.OK, 2, null];
        yield return ["lines-any-nested-and", "Lines any (Quantity>2,Name=='HighQty1')", false, HttpStatusCode.OK, 1, null];
        yield return ["businessdate-invalid", "BusinessDate==not-a-date", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to DateOnly"];
        yield return ["businesstime-invalid", "BusinessTime==not-a-time", false, HttpStatusCode.BadRequest, (int?)null, "could not be converted to TimeOnly"];
        yield return ["nested-missing-closing", "Lines any (Quantity>2", false, HttpStatusCode.BadRequest, (int?)null, "Missing closing bracket."];
    }

    public static IEnumerable<object?[]> MenuClauseCases()
    {
        yield return ["missing-property", new[] { new FilterClausePayload("", "eq", "Alpha") }, HttpStatusCode.BadRequest, (int?)null, "Property name is required."];
        yield return ["missing-operator", new[] { new FilterClausePayload("Name", "", "Alpha") }, HttpStatusCode.BadRequest, (int?)null, "Operator is required."];
        yield return ["null-with-gt", new[] { new FilterClausePayload("Price", "gt", "null") }, HttpStatusCode.BadRequest, (int?)null, "does not allow null values"];
        yield return ["isnull-on-value-type", new[] { new FilterClausePayload("Price", "isnull", null) }, HttpStatusCode.BadRequest, (int?)null, "does not allow null values"];
        yield return ["updatedat-isnull", new[] { new FilterClausePayload("UpdatedAt", "isnull", null) }, HttpStatusCode.OK, 3, null];
        yield return ["updatedat-notnull", new[] { new FilterClausePayload("UpdatedAt", "notnull", null) }, HttpStatusCode.OK, 1, null];
        yield return ["updatedat-eq-null", new[] { new FilterClausePayload("UpdatedAt", "eq", "null") }, HttpStatusCode.OK, 3, null];
        yield return ["updatedat-gt-null", new[] { new FilterClausePayload("UpdatedAt", "gt", "null") }, HttpStatusCode.BadRequest, (int?)null, "does not allow null values"];
        yield return ["name-in-list", new[] { new FilterClausePayload("Name", "in", "Alpha,Beta") }, HttpStatusCode.OK, 2, null];
        yield return ["price-between", new[] { new FilterClausePayload("Price", "between", "10,15") }, HttpStatusCode.OK, 2, null];
        yield return ["price-between-invalid-count", new[] { new FilterClausePayload("Price", "between", "10") }, HttpStatusCode.BadRequest, (int?)null, "Between requires two values."];
        yield return ["price-between-invalid-conversion", new[] { new FilterClausePayload("Price", "between", "10,abc") }, HttpStatusCode.BadRequest, (int?)null, "could not be converted to Decimal."];
        yield return ["name-any-scalar", new[] { new FilterClausePayload("Name", "any", "Alpha") }, HttpStatusCode.BadRequest, (int?)null, "only valid for collection properties."];
    }

    public static IEnumerable<object?[]> OrderClauseCases()
    {
        yield return ["status-in-list", new[] { new FilterClausePayload("Status", "in", "Paid,Failed") }, HttpStatusCode.OK, 2, null];
        yield return ["status-in-invalid", new[] { new FilterClausePayload("Status", "in", "Paid,UnknownStatus") }, HttpStatusCode.BadRequest, (int?)null, "could not be converted to OrderStatus."];
        yield return ["paymentid-eq-null", new[] { new FilterClausePayload("PaymentId", "eq", "null") }, HttpStatusCode.OK, 0, null];
        yield return ["paymentid-gt-null", new[] { new FilterClausePayload("PaymentId", "gt", "null") }, HttpStatusCode.BadRequest, (int?)null, "does not allow null values."];
        yield return ["paymentids-any-guid", new[] { new FilterClausePayload("PaymentIds", "any", "{order1PaymentId}") }, HttpStatusCode.OK, 1, null];
        yield return ["paymentids-any-invalid-guid", new[] { new FilterClausePayload("PaymentIds", "any", "not-a-guid") }, HttpStatusCode.BadRequest, (int?)null, "could not be converted to Guid."];
        yield return ["lines-any-nested", new[] { new FilterClausePayload("Lines", "any", "Quantity>2") }, HttpStatusCode.OK, 1, null];
        yield return ["lines-any-invalid-nested", new[] { new FilterClausePayload("Lines", "any", "Quantity has 2") }, HttpStatusCode.BadRequest, (int?)null, "could not be converted to OrderLine."];
    }

    private static async Task<MenuSeedContext> SeedMenuItemsAsync(HttpClient client)
    {
        var alpha = await CreateMenuItemAsync(client, "Alpha", "Main", 10);
        var beta = await CreateMenuItemAsync(client, "Beta", "Main", 25);
        await CreateMenuItemAsync(client, "Cola", "Drinks", 40);
        await CreateMenuItemAsync(client, "Al'pha", "Main", 15);

        beta.Price = 26;
        var updateResponse = await client.PutAsJsonAsync($"/api/menu-items/{beta.Id}", beta);
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, updateBody);
        var updated = await updateResponse.Content.ReadFromJsonAsync<MenuItem>();
        updated.ShouldNotBeNull();
        updated!.UpdatedAt.ShouldNotBeNull();

        var updatedAt = updated.UpdatedAt!.Value;
        return new MenuSeedContext(
            AlphaId: alpha.Id,
            UpdatedAtIso: updatedAt.ToString("o"),
            UpdatedAtMinIso: updatedAt.AddMinutes(-2).ToString("o"),
            UpdatedAtMaxIso: updatedAt.AddMinutes(2).ToString("o"));
    }

    private static async Task<OrderSeedContext> SeedOrdersAsync(HttpClient client)
    {
        var first = await CreateOrderAsync(
            client,
            "first@local.test",
            [new OrderLine { MenuItemId = Guid.NewGuid(), Name = "LowQty", UnitPrice = 10, Quantity = 1 }]);

        var second = await CreateOrderAsync(
            client,
            "second@local.test",
            [
                new OrderLine { MenuItemId = Guid.NewGuid(), Name = "HighQty1", UnitPrice = 12, Quantity = 3 },
                new OrderLine { MenuItemId = Guid.NewGuid(), Name = "HighQty2", UnitPrice = 9, Quantity = 4 }
            ]);

        first.PaymentId.ShouldNotBeNull();
        second.PaymentId.ShouldNotBeNull();
        return new OrderSeedContext(
            Order1PaymentId: first.PaymentId!.Value,
            Order2PaymentId: second.PaymentId!.Value,
            Order1BusinessDate: first.BusinessDate.ToString("yyyy-MM-dd"),
            Order2BusinessDate: second.BusinessDate.ToString("yyyy-MM-dd"),
            Order1BusinessTime: first.BusinessTime.ToString("HH:mm:ss"),
            Order2BusinessTime: second.BusinessTime.ToString("HH:mm:ss"));
    }

    private static string ReplaceTokens(string template, MenuSeedContext seeded)
    {
        return template
            .Replace("{alphaId}", seeded.AlphaId.ToString(), StringComparison.Ordinal)
            .Replace("{updatedAtIso}", seeded.UpdatedAtIso, StringComparison.Ordinal)
            .Replace("{updatedAtMinIso}", seeded.UpdatedAtMinIso, StringComparison.Ordinal)
            .Replace("{updatedAtMaxIso}", seeded.UpdatedAtMaxIso, StringComparison.Ordinal);
    }

    private static string ReplaceTokens(string template, OrderSeedContext seeded)
    {
        return template
            .Replace("{order1PaymentId}", seeded.Order1PaymentId.ToString(), StringComparison.Ordinal)
            .Replace("{order2PaymentId}", seeded.Order2PaymentId.ToString(), StringComparison.Ordinal)
            .Replace("{order1BusinessDate}", seeded.Order1BusinessDate, StringComparison.Ordinal)
            .Replace("{order2BusinessDate}", seeded.Order2BusinessDate, StringComparison.Ordinal)
            .Replace("{order1BusinessTime}", seeded.Order1BusinessTime, StringComparison.Ordinal)
            .Replace("{order2BusinessTime}", seeded.Order2BusinessTime, StringComparison.Ordinal);
    }

    private static FilterClausePayload[] ReplaceTokens(FilterClausePayload[] clauses, OrderSeedContext seeded)
    {
        return clauses
            .Select(clause => clause with { Value = clause.Value is null ? null : ReplaceTokens(clause.Value, seeded) })
            .ToArray();
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

    private sealed record MenuSeedContext(Guid AlphaId, string UpdatedAtIso, string UpdatedAtMinIso, string UpdatedAtMaxIso);
    private sealed record OrderSeedContext(
        Guid Order1PaymentId,
        Guid Order2PaymentId,
        string Order1BusinessDate,
        string Order2BusinessDate,
        string Order1BusinessTime,
        string Order2BusinessTime);

    public sealed record FilterBuilderRequest(
        string? Filter = null,
        FilterClausePayload[]? Clauses = null,
        string[]? AllowedProperties = null,
        bool? Strict = null,
        bool? CaseInsensitive = null);

    public sealed record FilterClausePayload(string Property, string Operator, string? Value);
}
