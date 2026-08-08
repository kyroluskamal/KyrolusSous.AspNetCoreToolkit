namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    private const string BetweenOperator = "between";
    private static readonly string[] BetweenSeparators = ["..", ",", "|"];

    private sealed record BetweenSpec(string Property, string Value, int ExpectedCount, Action<List<Product>> Assert);
    private sealed record ReviewBetweenSpec(string Property, string Value, int ExpectedCount, Action<List<Review>> Assert);
    private sealed record InvalidSpec(string Property, string Value, string? MessageContains);

    private static readonly IReadOnlyDictionary<string, BetweenSpec> ProductBetweenSpecs = BuildProductBetweenSpecs();
    private static readonly IReadOnlyDictionary<string, ReviewBetweenSpec> ReviewBetweenSpecs = BuildReviewBetweenSpecs();
    private static readonly IReadOnlyDictionary<string, InvalidSpec> InvalidProductBetweenSpecs = BuildInvalidProductBetweenSpecs();
    private static readonly IReadOnlyDictionary<string, InvalidSpec> InvalidReviewBetweenSpecs = BuildInvalidReviewBetweenSpecs();

    public static TheoryData<string> ProductBetweenCases => CaseIdsFrom(ProductBetweenSpecs);
    public static TheoryData<string> ReviewBetweenCases => CaseIdsFrom(ReviewBetweenSpecs);
    public static TheoryData<string> InvalidProductBetweenCases => CaseIdsFrom(InvalidProductBetweenSpecs);
    public static TheoryData<string> InvalidReviewBetweenCases => CaseIdsFrom(InvalidReviewBetweenSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports between operator across separators (products)")]
    [MemberData(nameof(ProductBetweenCases))]
    public Task GetAllIncludingDeletedAsync_Between_Operator_Works_ForProducts(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = ProductBetweenSpecs[caseId];
        var request = new QueryRequest(Filters: [new FilterClause(spec.Property, BetweenOperator, spec.Value)]);
        return AssertProducts(request, products =>
        {
            products.Count.ShouldBe(spec.ExpectedCount);
            spec.Assert(products);
        });
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports between operator across separators (reviews)")]
    [MemberData(nameof(ReviewBetweenCases))]
    public Task GetAllIncludingDeletedAsync_Between_Operator_Works_ForReviews(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = ReviewBetweenSpecs[caseId];
        var request = new QueryRequest(Filters: [new FilterClause(spec.Property, BetweenOperator, spec.Value)]);
        return AssertReviews(request, reviews =>
        {
            reviews.Count.ShouldBe(spec.ExpectedCount);
            spec.Assert(reviews);
        });
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync rejects invalid between filters (products)")]
    [MemberData(nameof(InvalidProductBetweenCases))]
    public async Task GetAllIncludingDeletedAsync_Between_InvalidValues_ReturnsError_ForProducts(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidProductBetweenSpecs[caseId];
        var request = new QueryRequest(
            Filters: [new FilterClause(spec.Property, BetweenOperator, spec.Value)],
            IncludeDeleted: true);

        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(request);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content.ShouldNotBeNull();
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains);
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync rejects invalid between filters (reviews)")]
    [MemberData(nameof(InvalidReviewBetweenCases))]
    public async Task GetAllIncludingDeletedAsync_Between_InvalidValues_ReturnsError_ForReviews(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidReviewBetweenSpecs[caseId];
        var request = new QueryRequest(
            Filters: [new FilterClause(spec.Property, BetweenOperator, spec.Value)],
            IncludeDeleted: true);

        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Review>(request);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content.ShouldNotBeNull();
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains);
    }

    private static IReadOnlyDictionary<string, BetweenSpec> BuildProductBetweenSpecs()
    {
        var data = new Dictionary<string, BetweenSpec>();
        foreach (var spec in GetProductSpecs())
        {
            foreach (var separator in BetweenSeparators)
            {
                var value = BuildBetweenValue(spec.Start, spec.End, separator);
                var caseId = $"{spec.Property}-{separator}-{spec.Start}-{spec.End}";
                data[caseId] = new BetweenSpec(spec.Property, value, spec.ExpectedCount, spec.Assert);
            }
        }
        return data;
    }

    private static IReadOnlyDictionary<string, ReviewBetweenSpec> BuildReviewBetweenSpecs()
    {
        var data = new Dictionary<string, ReviewBetweenSpec>();
        foreach (var spec in GetReviewSpecs())
        {
            foreach (var separator in BetweenSeparators)
            {
                var value = BuildBetweenValue(spec.Start, spec.End, separator);
                var caseId = $"{spec.Property}-{separator}-{spec.Start}-{spec.End}";
                data[caseId] = new ReviewBetweenSpec(spec.Property, value, spec.ExpectedCount, spec.Assert);
            }
        }
        return data;
    }

    private static IReadOnlyDictionary<string, InvalidSpec> BuildInvalidProductBetweenSpecs()
        => new Dictionary<string, InvalidSpec>
        {
            ["product-name"] = new InvalidSpec(nameof(Product.Name), "A..Z", "Invalid filter"),
            ["product-addedin"] = new InvalidSpec(nameof(Product.AddedIn), "2024-06-01", "Invalid filter"),
            ["product-price"] = new InvalidSpec(nameof(Product.Price), "100..abc", "Invalid filter")
        };

    private static IReadOnlyDictionary<string, InvalidSpec> BuildInvalidReviewBetweenSpecs()
        => new Dictionary<string, InvalidSpec>
        {
            ["review-comment"] = new InvalidSpec(nameof(Review.Comment), "A..Z", "Invalid filter"),
            ["review-addedin"] = new InvalidSpec(nameof(Review.AddedIn), "2024-06-01", "Invalid filter"),
            ["review-rating"] = new InvalidSpec(nameof(Review.Rating), "3..abc", "Invalid filter")
        };

    private static string BuildBetweenValue(string start, string end, string separator)
        => separator == ".." ? $"{start}..{end}" : $"{start}{separator}{end}";

    private static IEnumerable<(string Property, string Start, string End, int ExpectedCount, Action<List<Product>> Assert)> GetProductSpecs()
    {
        var expectedAddedIn = new[] { new DateOnly(2024, 6, 15), new DateOnly(2024, 8, 5) };
        var expectedAddedAt = new TimeOnly(10, 30);
        var expectedCreatedAtProducts = new[]
        {
            DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture)
        };
        var expectedDiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var expectedFinishedAt = TimeSpan.FromDays(1);

        yield return (
            nameof(Product.Price),
            "100",
            "300",
            1,
            products => products.Single().Price.ShouldBe(199m));

        yield return (
            nameof(Product.AddedIn),
            "2024-06-01",
            "2024-12-31",
            2,
            products => products.Select(p => p.AddedIn).OrderBy(x => x).ShouldBe(expectedAddedIn));

        yield return (
            nameof(Product.AddedAt),
            "10:00",
            "12:00",
            1,
            products => products.Single().AddedAt.ShouldBe(expectedAddedAt));

        yield return (
            nameof(Product.CreatedAt),
            "2024-06-01T00:00:00Z",
            "2024-12-31T00:00:00Z",
            2,
            products => products.Select(p => p.CreatedAt).OrderBy(x => x).ShouldBe(expectedCreatedAtProducts));

        yield return (
            nameof(Product.DiscontinuedAt),
            "2025-01-01T00:00:00Z",
            "2025-12-31T00:00:00Z",
            3,
            products => products.All(p => p.DiscontinuedAt == expectedDiscontinuedAt).ShouldBeTrue());

        yield return (
            nameof(Product.FinishedAt),
            "0.12:00:00",
            "1.12:00:00",
            2,
            products => products.All(p => p.FinishedAt == expectedFinishedAt).ShouldBeTrue());
    }

    private static IEnumerable<(string Property, string Start, string End, int ExpectedCount, Action<List<Review>> Assert)> GetReviewSpecs()
    {
        var expectedAddedIn = new[] { new DateOnly(2024, 6, 15), new DateOnly(2024, 8, 5) };
        var expectedAddedAt = new TimeOnly(10, 30);
        var expectedCreatedAtReviews = new[]
        {
            DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2025-03-01T00:00:00Z", CultureInfo.InvariantCulture)
        };
        var expectedDiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var expectedFinishedAt = TimeSpan.FromDays(1);

        yield return (
            nameof(Review.Rating),
            "3",
            "5",
            3,
            reviews => reviews.Select(r => r.Rating).OrderBy(x => x).ShouldBe([3, 4, 5]));

        yield return (
            nameof(Review.AddedIn),
            "2024-06-01",
            "2024-12-31",
            2,
            reviews => reviews.Select(r => r.AddedIn).OrderBy(x => x).ShouldBe(expectedAddedIn));

        yield return (
            nameof(Review.AddedAt),
            "10:00",
            "12:00",
            1,
            reviews => reviews.Single().AddedAt.ShouldBe(expectedAddedAt));

        yield return (
            nameof(Review.CreatedAt),
            "2025-01-01T00:00:00Z",
            "2025-03-01T00:00:00Z",
            3,
            reviews => reviews.Select(r => r.CreatedAt).OrderBy(x => x).ShouldBe(expectedCreatedAtReviews));

        yield return (
            nameof(Review.DiscontinuedAt),
            "2025-01-01T00:00:00Z",
            "2025-12-31T00:00:00Z",
            3,
            reviews => reviews.All(r => r.DiscontinuedAt == expectedDiscontinuedAt).ShouldBeTrue());

        yield return (
            nameof(Review.FinishedAt),
            "0.12:00:00",
            "1.12:00:00",
            2,
            reviews => reviews.All(r => r.FinishedAt == expectedFinishedAt).ShouldBeTrue());
    }
}
