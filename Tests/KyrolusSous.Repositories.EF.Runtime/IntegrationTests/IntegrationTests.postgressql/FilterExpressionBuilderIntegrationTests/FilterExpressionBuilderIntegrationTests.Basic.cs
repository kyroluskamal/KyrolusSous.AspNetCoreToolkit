using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.FilterExpressionBuilderIntegrationTests;

public sealed class FilterExpressionBuilderIntegrationTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private enum ProbeStatus
    {
        Draft = 1,
        Published = 2
    }

    private sealed class ProbeChild
    {
        public int Score { get; init; }
        public string? Label { get; init; }
    }

    private sealed class ProbeEntity
    {
        public string Name { get; init; } = string.Empty;
        public string[]? Tags { get; init; }
        public int[]? Scores { get; init; }
        public List<ProbeChild>? Children { get; init; }
        public DateOnly Day { get; init; }
        public TimeOnly Time { get; init; }
        public ProbeStatus Status { get; init; }
        public int? Optional { get; init; }
    }

    private static readonly IReadOnlyList<ProbeEntity> Probes =
    [
        new ProbeEntity
        {
            Name = "Alpha",
            Tags = ["HELLO", "API"],
            Scores = [5, 5],
            Children = [new ProbeChild { Score = 5, Label = "Top" }],
            Day = new DateOnly(2024, 06, 15),
            Time = new TimeOnly(10, 30),
            Status = ProbeStatus.Published,
            Optional = 10
        },
        new ProbeEntity
        {
            Name = "Beta",
            Tags = ["world"],
            Scores = [3, 5],
            Children = [new ProbeChild { Score = 3, Label = "Mid" }],
            Day = new DateOnly(2024, 08, 05),
            Time = new TimeOnly(14, 00),
            Status = ProbeStatus.Draft,
            Optional = null
        },
        new ProbeEntity
        {
            Name = "Gamma",
            Tags = null,
            Scores = null,
            Children = null,
            Day = new DateOnly(2025, 01, 01),
            Time = new TimeOnly(09, 00),
            Status = ProbeStatus.Published,
            Optional = 5
        }
    ];

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
        { "and-with-comma", "Price >= 100,Price <= 300", false, 1, ["Noise Cancelling Headphones"] }
    };

    public static TheoryData<string, string, bool, int> ProbeSuccessCases => new()
    {
        { "string-in-case-insensitive-on-probe", "Name in [\"alpha\",\"beta\"]", true, 2 },
        { "array-any-string-case-insensitive", "Tags any hello", true, 1 },
        { "array-all-int-list", "Scores all 5", false, 1 },
        { "array-any-int-list", "Scores any 3", false, 1 },
        { "children-any-filter", "Children any (Score >= 5)", false, 1 },
        { "children-all-filter", "Children all (Score >= 3)", false, 2 },
        { "enum-parse-success", "Status == Published", false, 2 },
        { "dateonly-parse-success", "Day == 2024-06-15", false, 1 },
        { "timeonly-parse-success", "Time == 10:30", false, 1 },
        { "nullable-null-check", "Optional isnull", false, 1 },
        { "nullable-not-null-check", "Optional notnull", false, 2 }
    };

    public static TheoryData<string, string, bool, string> InvalidFilterCases => new()
    {
        { "parse-or-trailing", "Name == \"Alpha\" |", false, "Property name is required" },
        { "parse-and-trailing", "Name == \"Alpha\",", false, "Property name is required" },
        { "unsupported-operator", "Name has code", false, "not supported" },
        { "null-op-on-nonnullable", "Day isnull", false, "does not allow null values" },
        { "null-literal-on-nonnullable", "Day == null", false, "does not allow null values" },
        { "null-with-invalid-operator", "Name contains null", false, "does not allow null values" },
        { "between-requires-two-values", "Optional between 100", false, "Between requires two values" },
        { "between-invalid-conversion", "Optional between bad..200", false, "could not be converted" },
        { "missing-closing-bracket", "Name in [a,b", false, "Missing closing bracket" },
        { "missing-closing-quote", "Name == \"abc", false, "Missing closing quote" },
        { "member-access-missing-property", "NotFound == 1", false, "was not found" },
        { "any-on-non-collection", "Name any value", false, "only valid for collection properties" },
        { "nested-invalid-filter", "Children any (Score == bad)", false, "could not be converted" },
        { "in-nonnullable-with-null", "Day in [null,2024-06-15]", false, "does not support NULL" },
        { "enum-invalid-value", "Status == Unknown", false, "could not be converted" },
        { "dateonly-invalid-value", "Day == 2024-99-99", false, "could not be converted" },
        { "timeonly-invalid-value", "Time == 99:99", false, "could not be converted" }
    };

    [Theory(DisplayName = "FilterExpressionBuilder builds product expression for supported filters")]
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
            items.Select(item => item.Name).ToArray().ShouldBe(expectedNames.OrderBy(n => n).ToArray());
    }

    [Theory(DisplayName = "FilterExpressionBuilder builds probe expression for collection and conversion scenarios")]
    [MemberData(nameof(ProbeSuccessCases))]
    public void FilterExpressionBuilder_Probe_SupportedFilters_Work(
        string caseId,
        string filter,
        bool caseInsensitive,
        int expectedCount)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var expression = BuildValidExpression<ProbeEntity>(filter, caseInsensitive);
        var predicate = expression.Compile();

        var matched = Probes.Where(predicate).ToList();
        matched.Count.ShouldBe(expectedCount);
    }

    [Theory(DisplayName = "FilterExpressionBuilder returns validation errors for invalid filter syntax and values")]
    [MemberData(nameof(InvalidFilterCases))]
    public void FilterExpressionBuilder_InvalidFilters_ReturnError(
        string caseId,
        string filter,
        bool caseInsensitive,
        string expectedErrorContains)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<ProbeEntity>(
            filter,
            caseInsensitive,
            out var expression,
            out var error);

        ok.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
        error.ShouldContain(expectedErrorContains);
    }

    [Fact(DisplayName = "FilterExpressionBuilder returns true with null expression for blank filter text")]
    public void FilterExpressionBuilder_BlankFilter_ReturnsTrueWithNullExpression()
    {
        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<ProbeEntity>(
            "   ",
            caseInsensitive: false,
            out var expression,
            out var error);

        ok.ShouldBeTrue();
        expression.ShouldBeNull();
        error.ShouldBeNull();
    }

    [Fact(DisplayName = "FilterExpressionBuilder throws when string operators are used on non-string members")]
    public void FilterExpressionBuilder_StringOperatorOnNonString_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
        {
            _ = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<ProbeEntity>(
                "Optional contains 1",
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
