namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    private const string BetweenOperator = "between";
    private static readonly string[] BetweenSeparators = ["..", ",", "|"];

    private sealed record BetweenCaseSpec(
        KeyType KeyType,
        string Property,
        string Start,
        string End,
        int ExpectedCount,
        Action<List<Product>>? AssertProducts = null,
        Action<List<Review>>? AssertReviews = null);

    public sealed record BetweenCase(
        KeyType KeyType,
        string Property,
        string Value,
        int ExpectedCount,
        Action<List<Product>>? AssertProducts,
        Action<List<Review>>? AssertReviews);

    public static TheoryData<BetweenCase> BetweenCases
    {
        get
        {
            var data = new TheoryData<BetweenCase>();
            foreach (var spec in GetBetweenSpecs())
            {
                foreach (var separator in BetweenSeparators)
                {
                    var value = separator == ".."
                        ? $"{spec.Start}..{spec.End}"
                        : $"{spec.Start}{separator}{spec.End}";
                    data.Add(new BetweenCase(
                        spec.KeyType,
                        spec.Property,
                        value,
                        spec.ExpectedCount,
                        spec.AssertProducts,
                        spec.AssertReviews));
                }
            }
            return data;
        }
    }

    public static TheoryData<string, string> BetweenInvalidCases => new()
    {
        { nameof(Product.Name), "A..Z" },
        { nameof(Product.AddedIn), "2024-06-01" },
        { nameof(Product.Price), "100..abc" }
    };

    public static TheoryData<string, string> BetweenInvalidCasesComposite => new()
    {
        { nameof(Review.Comment), "A..Z" },
        { nameof(Review.AddedIn), "2024-06-01" },
        { nameof(Review.Rating), "3..abc" }
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports between operator across separators")]
    [MemberData(nameof(BetweenCases))]
    public async Task GetAllIncludingDeletedAsync_Between_Operator_Works(BetweenCase testCase)
    {
        var request = new QueryRequest(Filters: [new FilterClause(testCase.Property, BetweenOperator, testCase.Value)]);

        if (testCase.KeyType == KeyType.Single)
        {
            await TestSingleKey(testCase.KeyType, products =>
            {
                products.Count.ShouldBe(testCase.ExpectedCount);
                testCase.AssertProducts?.Invoke(products);
            }, request);
            return;
        }

        await TestCompositeKey(testCase.KeyType, reviews =>
        {
            reviews.Count.ShouldBe(testCase.ExpectedCount);
            testCase.AssertReviews?.Invoke(reviews);
        }, request);
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync rejects invalid between filters")]
    [MemberData(nameof(BetweenInvalidCases))]
    public void GetAllIncludingDeletedAsync_Between_InvalidValues_Throws(string property, string value)
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause(property, BetweenOperator, value)]));
        });
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync rejects invalid between filters for composite")]
    [MemberData(nameof(BetweenInvalidCasesComposite))]
    public void GetAllIncludingDeletedAsync_Between_InvalidValues_Throws_ForComposite(string property, string value)
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Review>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause(property, BetweenOperator, value)]));
        });
    }

    private static IEnumerable<BetweenCaseSpec> GetBetweenSpecs()
    {
        var expectedAddedIn = new[] { new DateOnly(2024, 6, 15), new DateOnly(2024, 8, 5) };
        var expectedAddedAt = new TimeOnly(10, 30);
        var expectedCreatedAtProducts = new[]
        {
            DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture)
        };
        var expectedCreatedAtReviews = new[]
        {
            DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2025-03-01T00:00:00Z", CultureInfo.InvariantCulture)
        };
        var expectedDiscontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var expectedFinishedAt = TimeSpan.FromDays(1);

        yield return new BetweenCaseSpec(
            KeyType.Single,
            nameof(Product.Price),
            "100",
            "300",
            ExpectedCount: 1,
            AssertProducts: products =>
            {
                products.Single().Price.ShouldBe(199m);
            });

        yield return new BetweenCaseSpec(
            KeyType.Composite,
            nameof(Review.Rating),
            "3",
            "5",
            ExpectedCount: 3,
            AssertReviews: reviews =>
            {
                reviews.Select(r => r.Rating).OrderBy(x => x).ShouldBe([3, 4, 5]);
            });

        yield return new BetweenCaseSpec(
            KeyType.Single,
            nameof(Product.AddedIn),
            "2024-06-01",
            "2024-12-31",
            ExpectedCount: 2,
            AssertProducts: products =>
            {
                products.Select(p => p.AddedIn).OrderBy(x => x).ShouldBe(expectedAddedIn);
            });

        yield return new BetweenCaseSpec(
            KeyType.Composite,
            nameof(Review.AddedIn),
            "2024-06-01",
            "2024-12-31",
            ExpectedCount: 2,
            AssertReviews: reviews =>
            {
                reviews.Select(r => r.AddedIn).OrderBy(x => x).ShouldBe(expectedAddedIn);
            });

        yield return new BetweenCaseSpec(
            KeyType.Single,
            nameof(Product.AddedAt),
            "10:00",
            "12:00",
            ExpectedCount: 1,
            AssertProducts: products =>
            {
                products.Single().AddedAt.ShouldBe(expectedAddedAt);
            });

        yield return new BetweenCaseSpec(
            KeyType.Composite,
            nameof(Review.AddedAt),
            "10:00",
            "12:00",
            ExpectedCount: 1,
            AssertReviews: reviews =>
            {
                reviews.Single().AddedAt.ShouldBe(expectedAddedAt);
            });

        yield return new BetweenCaseSpec(
            KeyType.Single,
            nameof(Product.CreatedAt),
            "2024-06-01T00:00:00Z",
            "2024-12-31T00:00:00Z",
            ExpectedCount: 2,
            AssertProducts: products =>
            {
                products.Select(p => p.CreatedAt).OrderBy(x => x).ShouldBe(expectedCreatedAtProducts);
            });

        yield return new BetweenCaseSpec(
            KeyType.Composite,
            nameof(Review.CreatedAt),
            "2025-01-01T00:00:00Z",
            "2025-03-01T00:00:00Z",
            ExpectedCount: 3,
            AssertReviews: reviews =>
            {
                reviews.Select(r => r.CreatedAt).OrderBy(x => x).ShouldBe(expectedCreatedAtReviews);
            });

        yield return new BetweenCaseSpec(
            KeyType.Single,
            nameof(Product.DiscontinuedAt),
            "2025-01-01T00:00:00Z",
            "2025-12-31T00:00:00Z",
            ExpectedCount: 3,
            AssertProducts: products =>
            {
                products.All(p => p.DiscontinuedAt == expectedDiscontinuedAt).ShouldBeTrue();
            });

        yield return new BetweenCaseSpec(
            KeyType.Composite,
            nameof(Review.DiscontinuedAt),
            "2025-01-01T00:00:00Z",
            "2025-12-31T00:00:00Z",
            ExpectedCount: 3,
            AssertReviews: reviews =>
            {
                reviews.All(r => r.DiscontinuedAt == expectedDiscontinuedAt).ShouldBeTrue();
            });

        yield return new BetweenCaseSpec(
            KeyType.Single,
            nameof(Product.FinishedAt),
            "0.12:00:00",
            "1.12:00:00",
            ExpectedCount: 2,
            AssertProducts: products =>
            {
                products.All(p => p.FinishedAt == expectedFinishedAt).ShouldBeTrue();
            });

        yield return new BetweenCaseSpec(
            KeyType.Composite,
            nameof(Review.FinishedAt),
            "0.12:00:00",
            "1.12:00:00",
            ExpectedCount: 2,
            AssertReviews: reviews =>
            {
                reviews.All(r => r.FinishedAt == expectedFinishedAt).ShouldBeTrue();
            });
    }
}
