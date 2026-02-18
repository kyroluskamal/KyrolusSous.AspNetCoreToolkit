using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.FilterExpressionBuilderIntegrationTests;

public sealed class FilterExpressionBuilderIntegrationTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, string, bool, int, string[]?> ProductSuccessCases => new()
    {
        { "string-eq-case-insensitive", "Name == \"clean code\"", true, 1, ["Clean Code"] },
        { "string-in-case-insensitive", "Name in [\"laptop pro 15\",\"clean code\"]", true, 2, ["Clean Code", "Laptop Pro 15"] },
        { "string-in-escaped-quoted", "Name in [\"Laptop\\ Pro 15\",\"Clean Code\"]", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "nullable-in-has-null", "Weight in [null,0.25]", false, 2, null },
        { "nullable-in-without-null", "Count in [5,10]", false, 2, null },
        { "nullable-between", "Weight between 0.10..0.30", false, 1, ["Noise Cancelling Headphones"] },
        { "any-nested-filter", "Reviews any (Rating >= 4)", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "all-nested-filter", "Reviews all (Rating >= 4)", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "or-with-parenthesis", "(Name == \"Clean Code\") | (Name == \"Laptop Pro 15\")", false, 2, ["Clean Code", "Laptop Pro 15"] },
        { "and-with-comma", "Price >= 100,Price <= 300", false, 1, ["Noise Cancelling Headphones"] },
        { "dateonly-eq", "AddedIn == 2024-06-15", false, 1, ["Laptop Pro 15"] },
        { "timeonly-eq", "AddedAt == 10:30", false, 1, ["Laptop Pro 15"] },
        { "nullable-isnull", "Count isnull", false, 1, ["Clean Code"] },
        { "nullable-notnull", "Count notnull", false, 2, ["Laptop Pro 15", "Noise Cancelling Headphones"] }
    };

    public static TheoryData<string, string, bool, int> PaymentSuccessCases => new()
    {
        { "enum-parse-success", "Status == Paid", false, 1 },
        { "enum-parse-case-insensitive", "Status == paid", true, 1 }
    };

    public static TheoryData<string, string, bool, string> ProductInvalidFilterCases => new()
    {
        { "parse-or-trailing", "Name == \"Alpha\" |", false, "Property name is required" },
        { "parse-and-trailing", "Name == \"Alpha\",", false, "Property name is required" },
        { "unsupported-operator", "Name has code", false, "not supported" },
        { "null-op-on-nonnullable", "AddedIn isnull", false, "does not allow null values" },
        { "null-literal-on-nonnullable", "AddedIn == null", false, "does not allow null values" },
        { "null-with-invalid-operator", "Name contains null", false, "does not allow null values" },
        { "between-requires-two-values", "Count between 100", false, "Between requires two values" },
        { "between-invalid-conversion", "Count between bad..200", false, "could not be converted" },
        { "missing-closing-bracket", "Name in [a,b", false, "Missing closing bracket" },
        { "missing-closing-quote", "Name == \"abc", false, "Missing closing quote" },
        { "member-access-missing-property", "NotFound == 1", false, "was not found" },
        { "any-on-non-collection", "Name any value", false, "only valid for collection properties" },
        { "nested-invalid-filter", "Reviews any (Rating == bad)", false, "could not be converted" },
        { "in-nonnullable-with-null", "AddedIn in [null,2024-06-15]", false, "does not support NULL" },
        { "dateonly-invalid-value", "AddedIn == 2024-99-99", false, "could not be converted" },
        { "timeonly-invalid-value", "AddedAt == 99:99", false, "could not be converted" }
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

    [Theory(DisplayName = "FilterExpressionBuilder returns validation errors for invalid product filter syntax and values")]
    [MemberData(nameof(ProductInvalidFilterCases))]
    public void FilterExpressionBuilder_Product_InvalidFilters_ReturnError(
        string caseId,
        string filter,
        bool caseInsensitive,
        string expectedErrorContains)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<Product>(
            filter,
            caseInsensitive,
            out var expression,
            out var error);

        ok.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
        error.ShouldContain(expectedErrorContains);
    }

    [Fact(DisplayName = "FilterExpressionBuilder returns true with null expression for blank product filter text")]
    public void FilterExpressionBuilder_BlankFilter_ReturnsTrueWithNullExpression()
    {
        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<Product>(
            "   ",
            caseInsensitive: false,
            out var expression,
            out var error);

        ok.ShouldBeTrue();
        expression.ShouldBeNull();
        error.ShouldBeNull();
    }

    [Fact(DisplayName = "FilterExpressionBuilder throws when string operators are used on non-string product members")]
    public void FilterExpressionBuilder_StringOperatorOnNonString_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
        {
            _ = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<Product>(
                "Count contains 1",
                caseInsensitive: false,
                out _,
                out _);
        });
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
