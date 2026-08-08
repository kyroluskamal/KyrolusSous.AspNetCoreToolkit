namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class KyrolusFilterExpressionBuilderUnitTests
{
    public static TheoryData<string, string, bool, string> ProductInvalidFilterCases => new()
    {
        { "parse-or-trailing", "Name == \"Alpha\" |", false, "Property name is required" },
        { "parse-and-trailing", "Name == \"Alpha\",", false, "Property name is required" },
        { "missing-closing-parenthesis", "(Name == \"Alpha\"", false, "Missing closing ')'" },
        { "unsupported-operator", "Name has code", false, "not supported" },
        { "null-op-on-nonnullable", "AddedIn isnull", false, "does not allow null values" },
        { "null-literal-on-nonnullable", "AddedIn == null", false, "does not allow null values" },
        { "null-with-invalid-operator", "Name contains null", false, "does not allow null values" },
        { "between-requires-two-values", "Count between 100", false, "Between requires two values" },
        { "between-quoted-without-brackets", "Name between \"Clean Code\"..\"Laptop Pro 15\"", false, "Between requires two values" },
        { "between-single-quoted-without-brackets", "Name between 'Clean Code'..'Laptop Pro 15'", false, "Between requires two values" },
        { "between-invalid-conversion", "Count between bad..200", false, "could not be converted" },
        { "missing-closing-bracket", "Name in [a,b", false, "Missing closing bracket" },
        { "missing-closing-quote", "Name == \"abc", false, "Missing closing quote" },
        { "property-path-empty-segments", ". == 1", false, "Property name is required" },
        { "member-access-missing-property", "NotFound == 1", false, "was not found" },
        { "any-on-non-collection", "Name any value", false, "only valid for collection properties" },
        { "nested-invalid-filter", "Reviews any (Rating == bad)", false, "could not be converted" },
        { "nested-values-invalid-conversion", "Reviews any 5", false, "could not be converted to Review" },
        { "nested-values-with-pipe-invalid", "Reviews any A|B", false, "could not be converted to Review" },
        { "in-nonnullable-with-null", "AddedIn in [null,2024-06-15]", false, "does not support NULL" },
        { "guid-invalid-value", "Id == not-a-guid", false, "could not be converted" },
        { "datetimeoffset-invalid-value", "CreatedAt == not-a-dto", false, "could not be converted" },
        { "datetime-invalid-value", "DiscontinuedAt == not-a-date", false, "could not be converted" },
        { "rowversion-any-invalid-value", "RowVersion any bad", false, "could not be converted" },
        { "dateonly-invalid-value", "AddedIn == 2024-99-99", false, "could not be converted" },
        { "timeonly-invalid-value", "AddedAt == 99:99", false, "could not be converted" }
    };

    public static TheoryData<string, string, bool, string> PaymentInvalidFilterCases => new()
    {
        { "enum-invalid-value", "Status == UnknownStatus", false, "could not be converted" }
    };

    [Theory(DisplayName = "FilterExpressionBuilder returns validation errors for invalid product filter syntax and values")]
    [MemberData(nameof(ProductInvalidFilterCases))]
    public void FilterExpressionBuilder_Product_InvalidFilters_ReturnError(
        string caseId,
        string filter,
        bool caseInsensitive,
        string expectedErrorContains)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<FilterEntity>(
            filter,
            caseInsensitive,
            out var expression,
            out var error);

        ok.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
        error.ShouldContain(expectedErrorContains);
    }

    [Theory(DisplayName = "FilterExpressionBuilder returns validation errors for invalid enum filter values")]
    [MemberData(nameof(PaymentInvalidFilterCases))]
    public void FilterExpressionBuilder_Payment_InvalidFilters_ReturnError(
        string caseId,
        string filter,
        bool caseInsensitive,
        string expectedErrorContains)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<PaymentEntity>(
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
        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<FilterEntity>(
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
            _ = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<FilterEntity>(
                "Count contains 1",
                caseInsensitive: false,
                out _,
                out _);
        });
    }

    [Theory(DisplayName = "FilterExpressionBuilder throws for string between filters when both bounds are quoted values")]
    [InlineData("Name between [\"Clean Code\"..\"Laptop Pro 15\"]")]
    [InlineData("Name between [\"Clean\\ Code\"..\"Laptop\\ Pro\\ 15\"]")]
    public void FilterExpressionBuilder_StringBetweenQuotedBounds_ThrowsInvalidOperation(string filter)
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            _ = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<FilterEntity>(
                filter,
                caseInsensitive: false,
                out _,
                out _);
        });
    }

    [Fact(DisplayName = "FilterExpressionBuilder supports any operator on string collections with case-insensitive literal list")]
    public void FilterExpressionBuilder_StringCollectionAny_LiteralList_CaseInsensitive_Works()
    {
        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<TagContainer>(
            "Tags any alpha",
            caseInsensitive: true,
            out var expression,
            out var error);

        ok.ShouldBeTrue(error ?? "Expected string collection ANY filter to build.");
        expression.ShouldNotBeNull();

        var predicate = expression!.Compile();
        predicate(new TagContainer { Tags = ["ALPHA"] }).ShouldBeTrue();
        predicate(new TagContainer { Tags = ["gamma"] }).ShouldBeFalse();
        predicate(new TagContainer { Tags = null }).ShouldBeFalse();
    }

    [Fact(DisplayName = "FilterExpressionBuilder supports all operator on numeric collections with literal list")]
    public void FilterExpressionBuilder_NumericCollectionAll_LiteralList_Works()
    {
        var ok = KyrolusFilterExpressionBuilder.TryBuildFilterExpression<NumberContainer>(
            "Values all 1",
            caseInsensitive: false,
            out var expression,
            out var error);

        ok.ShouldBeTrue(error ?? "Expected numeric collection ALL filter to build.");
        expression.ShouldNotBeNull();

        var predicate = expression!.Compile();
        predicate(new NumberContainer { Values = [1, 1] }).ShouldBeTrue();
        predicate(new NumberContainer { Values = [1, 4] }).ShouldBeFalse();
        predicate(new NumberContainer { Values = null }).ShouldBeFalse();
    }

    private sealed class FilterEntity
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int? Count { get; init; }
        public decimal? Weight { get; init; }
        public DateOnly AddedIn { get; init; }
        public TimeOnly AddedAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTime? DiscontinuedAt { get; init; }
        public byte[] RowVersion { get; init; } = [];
        public List<Review>? Reviews { get; init; }
    }

    private sealed class Review
    {
        public int Rating { get; init; }
        public string? Comment { get; init; }
    }

    private sealed class PaymentEntity
    {
        public PaymentStatus Status { get; init; }
    }

    private enum PaymentStatus
    {
        Pending = 0,
        Paid = 1
    }

    private sealed class TagContainer
    {
        public List<string>? Tags { get; init; }
    }

    private sealed class NumberContainer
    {
        public List<int>? Values { get; init; }
    }
}
