using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using Shouldly;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

public sealed class RuntimeFilterExpressionBuilderIntegrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Theory(DisplayName = "Runtime filter expression builder - menu scalar parser matrix")]
    [MemberData(nameof(MenuExpressionCases))]
    public async Task Runtime_filter_expression_builder_menu_scalar_parser_matrix(
        string caseName,
        string filterTemplate,
        bool caseInsensitive,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"runtime-filter-menu-{NormalizeCaseName(caseName)}"));
        var seeded = await SeedMenuItemsAsync(client);
        var filter = ReplaceMenuTokens(filterTemplate, seeded);

        var response = await client.PostAsJsonAsync(
            "/api/menu-items/diagnostics/runtime-filter-expression-builder",
            new RuntimeFilterRequest(Filter: filter, CaseInsensitive: caseInsensitive));

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

    [Theory(DisplayName = "Runtime filter expression builder - order nested any/all parser matrix")]
    [MemberData(nameof(OrderExpressionCases))]
    public async Task Runtime_filter_expression_builder_order_nested_any_all_parser_matrix(
        string caseName,
        string filterTemplate,
        bool caseInsensitive,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment)
    {
        using var client = factory.CreateClientWithTenant(TestHelpers.NewTenantId($"runtime-filter-order-{NormalizeCaseName(caseName)}"));
        var token = await client.GetAccessTokenAsync("admin", "admin123");
        client.SetBearerToken(token);

        var seeded = await SeedOrdersAsync(client);
        var filter = ReplaceOrderTokens(filterTemplate, seeded);

        var response = await client.PostAsJsonAsync(
            "/api/orders/diagnostics/runtime-filter-expression-builder",
            new RuntimeFilterRequest(Filter: filter, CaseInsensitive: caseInsensitive));

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

    public static IEnumerable<object[]> MenuExpressionCases()
    {
        var eqOps = new[] { "eq", "==", "=" };
        var neqOps = new[] { "neq", "!=", "<>" };
        var gtOps = new[] { "gt", ">" };
        var gteOps = new[] { "gte", ">=" };
        var ltOps = new[] { "lt", "<" };
        var lteOps = new[] { "lte", "<=" };

        foreach (var op in eqOps)
        {
            yield return MenuCase($"category-main-{OperatorToken(op)}", $"Category {op} Main", HttpStatusCode.OK, 3);
            yield return MenuCase($"price-eq-10-{OperatorToken(op)}", $"Price {op} 10", HttpStatusCode.OK, 1);
        }

        foreach (var op in neqOps)
        {
            yield return MenuCase($"category-not-main-{OperatorToken(op)}", $"Category {op} Main", HttpStatusCode.OK, 1);
            yield return MenuCase($"id-not-alpha-{OperatorToken(op)}", $"Id {op} {{alphaId}}", HttpStatusCode.OK, 3);
        }

        foreach (var op in gtOps)
        {
            yield return MenuCase($"price-gt20-{OperatorToken(op)}", $"Price {op} 20", HttpStatusCode.OK, 2);
        }

        foreach (var op in gteOps)
        {
            yield return MenuCase($"price-gte26-{OperatorToken(op)}", $"Price {op} 26", HttpStatusCode.OK, 2);
        }

        foreach (var op in ltOps)
        {
            yield return MenuCase($"price-lt20-{OperatorToken(op)}", $"Price {op} 20", HttpStatusCode.OK, 2);
        }

        foreach (var op in lteOps)
        {
            yield return MenuCase($"price-lte15-{OperatorToken(op)}", $"Price {op} 15", HttpStatusCode.OK, 2);
        }

        yield return MenuCase("updatedat-isnull", "UpdatedAt isnull", HttpStatusCode.OK, 3);
        yield return MenuCase("updatedat-notnull", "UpdatedAt notnull", HttpStatusCode.OK, 1);
        yield return MenuCase("updatedat-eq-null", "UpdatedAt==null", HttpStatusCode.OK, 3);
        yield return MenuCase("updatedat-neq-null", "UpdatedAt!=null", HttpStatusCode.OK, 1);
        yield return MenuCase("updatedat-in-null-only", "UpdatedAt in [null]", HttpStatusCode.OK, 3);
        yield return MenuCase("updatedat-in-null-or-updated", "UpdatedAt in [null,{updatedAtIso}]", HttpStatusCode.OK, 4);
        yield return MenuCase("updatedat-in-single-updated", "UpdatedAt in [{updatedAtIso}]", HttpStatusCode.OK, 1);
        yield return MenuCase("updatedat-in-singlequoted", "UpdatedAt in ['{updatedAtIso}']", HttpStatusCode.OK, 1);
        yield return MenuCase("name-eq-case-insensitive-doublequoted", "Name==\"alpha\"", HttpStatusCode.OK, 1, caseInsensitive: true);
        yield return MenuCase("name-neq-case-insensitive-singlequoted", "Name!='alpha'", HttpStatusCode.OK, 3, caseInsensitive: true);
        yield return MenuCase("name-contains-ol", "Name contains ol", HttpStatusCode.BadRequest, null, "does not (yet) support member string.Contains");
        yield return MenuCase("name-startswith-al", "Name startswith Al", HttpStatusCode.BadRequest, null, "does not (yet) support member string.StartsWith");
        yield return MenuCase("name-endswith-a", "Name endswith a", HttpStatusCode.BadRequest, null, "does not (yet) support member string.EndsWith");
        yield return MenuCase("name-in-square", "Name in [Alpha,Beta]", HttpStatusCode.OK, 2);
        yield return MenuCase("name-in-round-pipe", "Name in (Alpha|Cola)", HttpStatusCode.OK, 2);
        yield return MenuCase("name-in-braces", "Name in {Alpha,Beta}", HttpStatusCode.OK, 2);
        yield return MenuCase("name-in-quoted-escape", "Name in ['Al\\'pha','Beta']", HttpStatusCode.OK, 2);
        yield return MenuCase("name-in-case-insensitive", "Name in [alpha,beta]", HttpStatusCode.OK, 2, caseInsensitive: true);
        yield return MenuCase("price-between-dotdot", "Price between 10..26", HttpStatusCode.OK, 3);
        yield return MenuCase("price-between-square", "Price between [10,26]", HttpStatusCode.OK, 3);
        yield return MenuCase("price-between-braces", "Price between {10,26}", HttpStatusCode.OK, 3);
        yield return MenuCase("createdat-between-wide", "CreatedAt between 2000-01-01T00:00:00Z..2100-01-01T00:00:00Z", HttpStatusCode.OK, 4);
        yield return MenuCase("updatedat-between-range", "UpdatedAt between [{updatedAtMinIso},{updatedAtMaxIso}]", HttpStatusCode.OK, 1);
        yield return MenuCase("updatedat-between-paren", "UpdatedAt between ({updatedAtMinIso}|{updatedAtMaxIso})", HttpStatusCode.OK, 1);
        yield return MenuCase("grouped-and", "(Category==Main,Price>=26)", HttpStatusCode.OK, 1);
        yield return MenuCase("grouped-or", "(Category==Main|Category==Drinks)", HttpStatusCode.OK, 4);
        yield return MenuCase("or-with-escaped-quoted", "Category==Drinks|Name=='Al\\'pha'", HttpStatusCode.OK, 2);

        yield return MenuCase("invalid-missing-property", "==Alpha", HttpStatusCode.BadRequest, null, "Property name is required.");
        yield return MenuCase("invalid-unsupported-operator", "Name has Alpha", HttpStatusCode.BadRequest, null, "Operator 'has' is not supported.");
        yield return MenuCase("invalid-null-on-nonnullable", "Price isnull", HttpStatusCode.BadRequest, null, "does not allow null values");
        yield return MenuCase("invalid-gt-null", "Price gt null", HttpStatusCode.BadRequest, null, "does not allow null values");
        yield return MenuCase("invalid-in-missing-bracket", "Name in [Alpha,Beta", HttpStatusCode.BadRequest, null, "Missing closing bracket.");
        yield return MenuCase("invalid-quoted-missing-close", "Name=='Alpha", HttpStatusCode.BadRequest, null, "Missing closing quote.");
        yield return MenuCase("invalid-group-missing-close", "(Category==Main", HttpStatusCode.BadRequest, null, "Missing closing ')'.");
        yield return MenuCase("invalid-between-single", "Price between 10", HttpStatusCode.BadRequest, null, "Between requires two values.");
        yield return MenuCase("invalid-price-conversion", "Price==bad", HttpStatusCode.BadRequest, null, "could not be converted to Decimal");
        yield return MenuCase("invalid-createdat-conversion", "CreatedAt==not-a-date", HttpStatusCode.BadRequest, null, "could not be converted to DateTimeOffset");
        yield return MenuCase("invalid-unknown-property", "Unknown==x", HttpStatusCode.BadRequest, null, "was not found");
        yield return MenuCase("invalid-contains-on-nonstring", "Price contains 1", HttpStatusCode.BadRequest, null, "only valid for string properties");
        yield return MenuCase("invalid-in-null-for-decimal", "Price in [10,null]", HttpStatusCode.BadRequest, null, "does not support NULL");
        yield return MenuCase("invalid-between-bad-end", "Price between 10..bad", HttpStatusCode.BadRequest, null, "could not be converted to Decimal");
        yield return MenuCase("invalid-operator-missing", "Name", HttpStatusCode.BadRequest, null, "Operator is required.");
    }

    public static IEnumerable<object[]> OrderExpressionCases()
    {
        var eqOps = new[] { "eq", "==", "=" };

        foreach (var op in eqOps)
        {
            yield return OrderCase($"status-paid-{OperatorToken(op)}", $"Status {op} Paid", HttpStatusCode.OK, 3);
            yield return OrderCase($"order1-id-{OperatorToken(op)}", $"Id {op} {{order1Id}}", HttpStatusCode.OK, 1);
        }

        yield return OrderCase("paymentids-any-order1", "PaymentIds any [{order1PaymentId}]", HttpStatusCode.OK, 1);
        yield return OrderCase("paymentarray-any-order2", "PaymentArrayIds any [{order2PaymentId}]", HttpStatusCode.OK, 1);
        yield return OrderCase("paymentset-any-order3", "PaymentSetIds any [{order3PaymentId}]", HttpStatusCode.OK, 1);
        yield return OrderCase("paymentid-notnull", "PaymentId notnull", HttpStatusCode.OK, 3);
        yield return OrderCase("paymentid-isnull", "PaymentId isnull", HttpStatusCode.OK, 0);
        yield return OrderCase("paymentid-in-null-only", "PaymentId in [null]", HttpStatusCode.OK, 0);
        yield return OrderCase("paymentid-in-null-or-order1", "PaymentId in [null,{order1PaymentId}]", HttpStatusCode.OK, 1);
        yield return OrderCase("paymentid-in-single-order1", "PaymentId in [{order1PaymentId}]", HttpStatusCode.OK, 1);
        yield return OrderCase("businessdate-order1", "BusinessDate=={order1BusinessDate}", HttpStatusCode.OK, 1);
        yield return OrderCase("businessdate-order1-doublequoted", "BusinessDate==\"{order1BusinessDate}\"", HttpStatusCode.OK, 1);
        yield return OrderCase("businessdate-order1-singlequoted", "BusinessDate=='{order1BusinessDate}'", HttpStatusCode.OK, 1);
        yield return OrderCase("businessdate-in-quoted", "BusinessDate in ['{order1BusinessDate}']", HttpStatusCode.OK, 1);
        yield return OrderCase("businessdate-between-wide", "BusinessDate between {businessDateMin}..{businessDateMax}", HttpStatusCode.OK, 3);
        yield return OrderCase("businesstime-order2", "BusinessTime=={order2BusinessTime}", HttpStatusCode.OK, 1);
        yield return OrderCase("businesstime-order2-singlequoted", "BusinessTime=='{order2BusinessTime}'", HttpStatusCode.OK, 1);
        yield return OrderCase("businesstime-in-quoted", "BusinessTime in ['{order1BusinessTime}','{order3BusinessTime}']", HttpStatusCode.OK, 2);
        yield return OrderCase("businesstime-in-edge-values", "BusinessTime in [{order1BusinessTime},{order3BusinessTime}]", HttpStatusCode.OK, 2);
        yield return OrderCase("businesstime-between-wide", "BusinessTime between {businessTimeMin}..{businessTimeMax}", HttpStatusCode.OK, 3);
        yield return OrderCase("businesstime-between-brackets", "BusinessTime between [{businessTimeMin},{businessTimeMax}]", HttpStatusCode.OK, 3);
        yield return OrderCase("status-not-failed", "Status!=Failed", HttpStatusCode.OK, 3);
        yield return OrderCase("status-eq-doublequoted", "Status==\"paid\"", HttpStatusCode.OK, 3);
        yield return OrderCase("status-neq-singlequoted", "Status!='FAILED'", HttpStatusCode.OK, 3);
        yield return OrderCase("status-in-paid-failed", "Status in [Paid,Failed]", HttpStatusCode.OK, 3);
        yield return OrderCase("status-in-singlequoted-mixedcase", "Status in ['paid','FAILED']", HttpStatusCode.OK, 3);
        yield return OrderCase("fulfillmentwindow-unsupported-timespan", "FulfillmentWindow==00:10:00", HttpStatusCode.BadRequest, null, "could not be converted to TimeSpan");
        yield return OrderCase("tags-any-highqty1", "Tags any [HighQty1]", HttpStatusCode.OK, 1);
        yield return OrderCase("tags-any-lower-sensitive", "Tags any [highqty1]", HttpStatusCode.OK, 0);
        yield return OrderCase("tags-any-lower-insensitive", "Tags any [highqty1]", HttpStatusCode.OK, 0, caseInsensitive: true);
        yield return OrderCase("tags-all-list", "Tags all [HighQty1,HighQty2]", HttpStatusCode.BadRequest, null, "Operator is required.");
        yield return OrderCase("tags-all-list-insensitive", "Tags all [highqty1,highqty2]", HttpStatusCode.BadRequest, null, "Operator is required.");
        yield return OrderCase("lines-any-quantity-gt2", "Lines any Quantity>2", HttpStatusCode.OK, 1);
        yield return OrderCase("lines-all-quantity-gte1", "Lines all Quantity>=1", HttpStatusCode.OK, 1);
        yield return OrderCase("lines-all-quantity-gt1", "Lines all Quantity>1", HttpStatusCode.OK, 0);
        yield return OrderCase("lines-any-grouped-or", "Lines any (Quantity>3|Quantity<2)", HttpStatusCode.OK, 2);
        yield return OrderCase("lines-any-grouped-and", "Lines any (Quantity>3,Quantity<5)", HttpStatusCode.OK, 1);
        yield return OrderCase("lines-any-date-between", "Lines any ScheduledDate between 2024-01-02..2024-01-03", HttpStatusCode.BadRequest, null, "could not be converted to OrderLine");
        yield return OrderCase("lines-any-time-gte", "Lines any ScheduledTime>=10:00", HttpStatusCode.OK, 2);
        yield return OrderCase("lines-any-name-eq", "Lines any Name=='HighQty1'", HttpStatusCode.OK, 1);
        yield return OrderCase("lines-any-name-eq-case-insensitive", "Lines any Name=='highqty1'", HttpStatusCode.OK, 0, caseInsensitive: true);
        yield return OrderCase("lines-any-name-in", "Lines any Name in ['HighQty1','MidQty']", HttpStatusCode.BadRequest, null, "could not be converted to OrderLine");
        yield return OrderCase("lines-any-quantity-in", "Lines any Quantity in [1,4]", HttpStatusCode.BadRequest, null, "could not be converted to OrderLine");
        yield return OrderCase("lines-any-quantity-between", "Lines any Quantity between 2..3", HttpStatusCode.BadRequest, null, "could not be converted to OrderLine");

        yield return OrderCase("invalid-any-on-scalar", "CustomerEmail any admin", HttpStatusCode.BadRequest, null, "only valid for collection properties");
        yield return OrderCase("invalid-paymentids-nested-prop", "PaymentIds any Quantity>1", HttpStatusCode.BadRequest, null, "was not found");
        yield return OrderCase("invalid-paymentarray-bad-guid", "PaymentArrayIds any [not-a-guid]", HttpStatusCode.BadRequest, null, "could not be converted to Guid");
        yield return OrderCase("invalid-businessdate-conversion", "BusinessDate==not-a-date", HttpStatusCode.BadRequest, null, "could not be converted to DateOnly");
        yield return OrderCase("invalid-businesstime-conversion", "BusinessTime==not-a-time", HttpStatusCode.BadRequest, null, "could not be converted to TimeOnly");
        yield return OrderCase("invalid-status-in-conversion", "Status in [Paid,Missing]", HttpStatusCode.BadRequest, null, "could not be converted to OrderStatus");
        yield return OrderCase("invalid-lines-quantity-isnull", "Lines any Quantity isnull", HttpStatusCode.BadRequest, null, "could not be converted to OrderLine");
        yield return OrderCase("invalid-lines-quantity-null-eq", "Lines any Quantity==null", HttpStatusCode.BadRequest, null, "does not allow null values");
        yield return OrderCase("invalid-lines-missing-close-paren", "Lines any (Quantity>2", HttpStatusCode.BadRequest, null, "Missing closing bracket.");
        yield return OrderCase("invalid-paymentids-missing-bracket", "PaymentIds any [{order1PaymentId}", HttpStatusCode.BadRequest, null, "Missing closing bracket.");
        yield return OrderCase("invalid-lines-missing-quote", "Lines any Name=='HighQty1", HttpStatusCode.BadRequest, null, "Missing closing quote.");
        yield return OrderCase("invalid-lines-unsupported-op", "Lines any Quantity has 1", HttpStatusCode.BadRequest, null, "could not be converted to OrderLine");
        yield return OrderCase("invalid-lines-unknown-property", "Lines any Missing>1", HttpStatusCode.BadRequest, null, "was not found");
        yield return OrderCase("invalid-lines-between-single", "Lines any Quantity between 2", HttpStatusCode.BadRequest, null, "could not be converted to OrderLine");
        yield return OrderCase("invalid-lines-in-with-null", "Lines any Quantity in [1,null]", HttpStatusCode.BadRequest, null, "could not be converted to OrderLine");
        yield return OrderCase("invalid-lines-list-like-filter", "Lines any [1,2]", HttpStatusCode.BadRequest, null, "Operator is required.");
        yield return OrderCase("invalid-lines-group-empty-second", "Lines any Quantity>2,", HttpStatusCode.BadRequest, null, null);
    }

    private static object[] MenuCase(
        string caseName,
        string filter,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment = null,
        bool caseInsensitive = false)
        => [caseName, filter, caseInsensitive, expectedStatus, expectedCount, expectedFragment];

    private static object[] OrderCase(
        string caseName,
        string filter,
        HttpStatusCode expectedStatus,
        int? expectedCount,
        string? expectedFragment = null,
        bool caseInsensitive = false)
        => [caseName, filter, caseInsensitive, expectedStatus, expectedCount, expectedFragment];

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
            UpdatedAtIso: updatedAt.ToString("o", CultureInfo.InvariantCulture),
            UpdatedAtMinIso: updatedAt.AddMinutes(-2).ToString("o", CultureInfo.InvariantCulture),
            UpdatedAtMaxIso: updatedAt.AddMinutes(2).ToString("o", CultureInfo.InvariantCulture));
    }

    private static async Task<OrderSeedContext> SeedOrdersAsync(HttpClient client)
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

        return new OrderSeedContext(
            Order1Id: first.Id,
            Order1PaymentId: first.PaymentId!.Value,
            Order2PaymentId: second.PaymentId!.Value,
            Order3PaymentId: third.PaymentId!.Value,
            Order1BusinessDate: first.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Order2BusinessTime: second.BusinessTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Order1BusinessTime: first.BusinessTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Order3BusinessTime: third.BusinessTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            BusinessDateMin: new[] { first.BusinessDate, second.BusinessDate, third.BusinessDate }.Min().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            BusinessDateMax: new[] { first.BusinessDate, second.BusinessDate, third.BusinessDate }.Max().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            BusinessTimeMin: new[] { first.BusinessTime, second.BusinessTime, third.BusinessTime }.Min().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            BusinessTimeMax: new[] { first.BusinessTime, second.BusinessTime, third.BusinessTime }.Max().ToString("HH:mm:ss", CultureInfo.InvariantCulture));
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

    private static string ReplaceMenuTokens(string template, MenuSeedContext seed)
    {
        return template
            .Replace("{alphaId}", seed.AlphaId.ToString(), StringComparison.Ordinal)
            .Replace("{updatedAtIso}", seed.UpdatedAtIso, StringComparison.Ordinal)
            .Replace("{updatedAtMinIso}", seed.UpdatedAtMinIso, StringComparison.Ordinal)
            .Replace("{updatedAtMaxIso}", seed.UpdatedAtMaxIso, StringComparison.Ordinal);
    }

    private static string ReplaceOrderTokens(string template, OrderSeedContext seed)
    {
        return template
            .Replace("{order1Id}", seed.Order1Id.ToString(), StringComparison.Ordinal)
            .Replace("{order1PaymentId}", seed.Order1PaymentId.ToString(), StringComparison.Ordinal)
            .Replace("{order2PaymentId}", seed.Order2PaymentId.ToString(), StringComparison.Ordinal)
            .Replace("{order3PaymentId}", seed.Order3PaymentId.ToString(), StringComparison.Ordinal)
            .Replace("{order1BusinessDate}", seed.Order1BusinessDate, StringComparison.Ordinal)
            .Replace("{order2BusinessTime}", seed.Order2BusinessTime, StringComparison.Ordinal)
            .Replace("{order1BusinessTime}", seed.Order1BusinessTime, StringComparison.Ordinal)
            .Replace("{order3BusinessTime}", seed.Order3BusinessTime, StringComparison.Ordinal)
            .Replace("{businessDateMin}", seed.BusinessDateMin, StringComparison.Ordinal)
            .Replace("{businessDateMax}", seed.BusinessDateMax, StringComparison.Ordinal)
            .Replace("{businessTimeMin}", seed.BusinessTimeMin, StringComparison.Ordinal)
            .Replace("{businessTimeMax}", seed.BusinessTimeMax, StringComparison.Ordinal);
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

    private sealed record MenuSeedContext(
        Guid AlphaId,
        string UpdatedAtIso,
        string UpdatedAtMinIso,
        string UpdatedAtMaxIso);

    private sealed record OrderSeedContext(
        Guid Order1Id,
        Guid Order1PaymentId,
        Guid Order2PaymentId,
        Guid Order3PaymentId,
        string Order1BusinessDate,
        string Order2BusinessTime,
        string Order1BusinessTime,
        string Order3BusinessTime,
        string BusinessDateMin,
        string BusinessDateMax,
        string BusinessTimeMin,
        string BusinessTimeMax);

    public sealed record RuntimeFilterRequest(string? Filter = null, bool? CaseInsensitive = null);
}
