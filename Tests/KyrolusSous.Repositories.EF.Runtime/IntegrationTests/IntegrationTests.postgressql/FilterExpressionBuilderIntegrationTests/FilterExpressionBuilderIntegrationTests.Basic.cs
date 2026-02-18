using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.FilterExpressionBuilderIntegrationTests;

public sealed class FilterExpressionBuilderIntegrationTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, string, bool, int, string[]?> ProductSuccessCases => new()
    {
        { "string-eq-case-insensitive", "Name == \"clean code\"", true, 1, ["Clean Code"] },
        { "guid-eq", $"Id == {DataSeeder.productLaptopId}", false, 1, ["Laptop Pro 15"] },
        { "string-eq-alias", "Name = \"Clean Code\"", false, 1, ["Clean Code"] },
        { "createdat-datetimeoffset-eq", "CreatedAt == 2024-06-01T00:00:00Z", false, 1, ["Laptop Pro 15"] },
        { "discontinuedat-datetime-eq", "DiscontinuedAt == 2025-12-31T00:00:00Z", false, 3, ["Clean Code", "Laptop Pro 15", "Noise Cancelling Headphones"] },
        { "bool-eq-true", "IsActive == true", false, 3, ["Clean Code", "Laptop Pro 15", "Noise Cancelling Headphones"] },
        { "string-in-case-insensitive", "Name in [\"laptop pro 15\",\"clean code\"]", true, 2, ["Clean Code", "Laptop Pro 15"] },
        { "string-in-escaped-quoted", "Name in [\"Laptop\\ Pro 15\",\"Clean Code\"]", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "nullable-in-has-null", "Weight in [null,0.25]", false, 2, null },
        { "nullable-in-without-null", "Count in [5,10]", false, 2, null },
        { "nullable-between", "Weight between 0.10..0.30", false, 1, ["Noise Cancelling Headphones"] },
        { "any-nested-filter", "Reviews any (Rating >= 4)", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "all-nested-filter", "Reviews all (Rating >= 4)", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "any-nested-eq-keyword", "Reviews any (Rating eq 5)", false, 1, ["Laptop Pro 15"] },
        { "any-nested-neq-keyword", "Reviews any (Rating neq 5)", false, 2, ["Clean Code", "Noise Cancelling Headphones"] },
        { "any-nested-gt-keyword", "Reviews any (Rating gt 4)", false, 1, ["Laptop Pro 15"] },
        { "any-nested-gte-keyword", "Reviews any (Rating gte 5)", false, 1, ["Laptop Pro 15"] },
        { "any-nested-lt-keyword", "Reviews any (Rating lt 4)", false, 1, ["Noise Cancelling Headphones"] },
        { "any-nested-lte-keyword", "Reviews any (Rating lte 3)", false, 1, ["Noise Cancelling Headphones"] },
        { "any-nested-contains-keyword", "Reviews any (Comment contains read)", true, 1, ["Clean Code"] },
        { "any-nested-startswith-keyword", "Reviews any (Comment startswith Great)", false, 1, ["Laptop Pro 15"] },
        { "any-nested-endswith-keyword", "Reviews any (Comment endswith concepts.)", false, 1, ["Clean Code"] },
        { "any-nested-in-keyword", "Reviews any (Rating in [3,5])", false, 2, ["Laptop Pro 15", "Noise Cancelling Headphones"] },
        { "any-nested-between-keyword", "Reviews any (Rating between 4..5)", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "or-with-parenthesis", "(Name == \"Clean Code\") | (Name == \"Laptop Pro 15\")", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "and-with-comma", "Price >= 100,Price <= 300", false, 1, ["Noise Cancelling Headphones"] },
        { "dateonly-eq", "AddedIn == 2024-06-15", false, 1, ["Laptop Pro 15"] },
        { "timeonly-eq", "AddedAt == 10:30", false, 1, ["Laptop Pro 15"] },
        { "nullable-eq-null", "Count == null", false, 1, ["Clean Code"] },
        { "nullable-neq-null", "Count != null", false, 2, ["Laptop Pro 15", "Noise Cancelling Headphones"] },
        { "nullable-neq-alias", "Count <> null", false, 2, ["Laptop Pro 15", "Noise Cancelling Headphones"] },
        { "nullable-isnull", "Count isnull", false, 1, ["Clean Code"] },
        { "nullable-notnull", "Count notnull", false, 2, ["Laptop Pro 15", "Noise Cancelling Headphones"] }
    };

    public static TheoryData<string, string, bool, int> PaymentSuccessCases => new()
    {
        { "enum-parse-success", "Status == Paid", false, 1 },
        { "enum-parse-case-insensitive", "Status == paid", true, 1 }
    };

    [Theory(DisplayName = "FilterExpressionBuilder builds product expression and applies it on real PostgreSQL query")]
    [MemberData(nameof(ProductSuccessCases))]
    public async Task FilterExpressionBuilder_Product_SupportedFilters_Work(
        string caseId,
        string filter,
        bool caseInsensitive,
        int expectedCount,
        string[]? expectedNames)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var expression = BuildValidExpression<Product>(filter, caseInsensitive);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var items = await db.Products.AsNoTracking().Where(expression).OrderBy(p => p.Name).ToListAsync();

        items.Count.ShouldBe(expectedCount);
        if (expectedNames is not null)
            items.Select(item => item.Name).ToArray().ShouldBe(expectedNames.OrderBy(static n => n).ToArray());
    }

    [Theory(DisplayName = "FilterExpressionBuilder builds payment expression and applies enum filters on real PostgreSQL query")]
    [MemberData(nameof(PaymentSuccessCases))]
    public async Task FilterExpressionBuilder_Payment_SupportedFilters_Work(
        string caseId,
        string filter,
        bool caseInsensitive,
        int expectedCount)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var expression = BuildValidExpression<Payment>(filter, caseInsensitive);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var items = await db.Payments.AsNoTracking().Where(expression).ToListAsync();

        items.Count.ShouldBe(expectedCount);
    }

    private static Expression<Func<TEntity, bool>> BuildValidExpression<TEntity>(string filter, bool caseInsensitive)
    {
        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<TEntity>(
            filter,
            caseInsensitive,
            out var expression,
            out var error);

        ok.ShouldBeTrue(error ?? "Expected expression build success.");
        expression.ShouldNotBeNull();
        return expression!;
    }

}
