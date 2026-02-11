namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    private static readonly string[] EqOps = ["eq", "=", "=="];
    private static readonly string[] NeqOps = ["neq", "!=", "<>"];
    private static readonly string[] GtOps = ["gt", ">"];
    private static readonly string[] GteOps = ["gte", ">="];
    private static readonly string[] LtOps = ["lt", "<"];
    private static readonly string[] LteOps = ["lte", "<="];

    private sealed record ProductOperatorSpec(FilterClause Filter, int ExpectedCount, Action<List<Product>> Assert);
    private sealed record ReviewOperatorSpec(FilterClause Filter, int ExpectedCount, Action<List<Review>> Assert);

    private static readonly IReadOnlyDictionary<string, ProductOperatorSpec> ProductOperatorSpecs = BuildProductOperatorSpecs();
    private static readonly IReadOnlyDictionary<string, ReviewOperatorSpec> ReviewOperatorSpecs = BuildReviewOperatorSpecs();

    public static TheoryData<string> ProductOperatorCases => CaseIdsFrom(ProductOperatorSpecs);
    public static TheoryData<string> ReviewOperatorCases => CaseIdsFrom(ReviewOperatorSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports filter operators for products")]
    [MemberData(nameof(ProductOperatorCases))]
    public Task GetAllIncludingDeletedAsync_Product_Operators_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = ProductOperatorSpecs[caseId];
        var request = new QueryRequest(Filters: [spec.Filter]);
        return AssertProducts(request, products =>
        {
            products.Count.ShouldBe(spec.ExpectedCount);
            spec.Assert(products);
        });
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports filter operators for reviews")]
    [MemberData(nameof(ReviewOperatorCases))]
    public Task GetAllIncludingDeletedAsync_Review_Operators_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = ReviewOperatorSpecs[caseId];
        var request = new QueryRequest(Filters: [spec.Filter]);
        return AssertReviews(request, reviews =>
        {
            reviews.Count.ShouldBe(spec.ExpectedCount);
            spec.Assert(reviews);
        });
    }

    private static IReadOnlyDictionary<string, ProductOperatorSpec> BuildProductOperatorSpecs()
    {
        var data = new Dictionary<string, ProductOperatorSpec>();

        foreach (var op in EqOps)
        {
            AddProductCase(data, $"eq-stockquantity-{op}", nameof(Product.StockQuantity), op, "50", 1,
                products => products.Single().StockQuantity.ShouldBe(50));
            AddProductCase(data, $"eq-price-{op}", nameof(Product.Price), op, "199", 1,
                products => products.Single().Price.ShouldBe(199m));
            AddProductCase(data, $"eq-isactive-{op}", nameof(Product.IsActive), op, "false", 0,
                products => products.Count.ShouldBe(0));
            AddProductCase(data, $"eq-id-{op}", nameof(Product.Id), op, DataSeeder.productLaptopId.ToString(), 1,
                products => products.Single().Id.ShouldBe(DataSeeder.productLaptopId));
            AddProductCase(data, $"eq-name-{op}", nameof(Product.Name), op, "Clean Code", 1,
                products => products.Single().Name.ShouldBe("Clean Code"));
            AddProductCase(data, $"eq-weight-null-{op}", nameof(Product.Weight), op, "null", 1,
                products => products.Single().Weight.ShouldBeNull());
            AddProductCase(data, $"eq-count-null-{op}", nameof(Product.Count), op, "null", 1,
                products => products.Single().Count.ShouldBeNull());
        }

        foreach (var op in NeqOps)
        {
            AddProductCase(data, $"neq-stockquantity-{op}", nameof(Product.StockQuantity), op, "50", 2,
                products => products.All(p => p.StockQuantity != 50).ShouldBeTrue());
            AddProductCase(data, $"neq-isactive-{op}", nameof(Product.IsActive), op, "false", 3,
                products => products.All(p => p.IsActive).ShouldBeTrue());
            AddProductCase(data, $"neq-id-{op}", nameof(Product.Id), op, DataSeeder.productLaptopId.ToString(), 2,
                products => products.Any(p => p.Id == DataSeeder.productLaptopId).ShouldBeFalse());
            AddProductCase(data, $"neq-name-{op}", nameof(Product.Name), op, "Clean Code", 2,
                products => products.Any(p => p.Name == "Clean Code").ShouldBeFalse());
        }

        foreach (var op in GtOps)
        {
            AddProductCase(data, $"gt-stockquantity-{op}", nameof(Product.StockQuantity), op, "25", 2,
                products => products.All(p => p.StockQuantity > 25).ShouldBeTrue());
        }

        foreach (var op in GteOps)
        {
            AddProductCase(data, $"gte-stockquantity-{op}", nameof(Product.StockQuantity), op, "50", 2,
                products => products.All(p => p.StockQuantity >= 50).ShouldBeTrue());
        }

        foreach (var op in LtOps)
        {
            AddProductCase(data, $"lt-stockquantity-{op}", nameof(Product.StockQuantity), op, "50", 1,
                products => products.All(p => p.StockQuantity < 50).ShouldBeTrue());
        }

        foreach (var op in LteOps)
        {
            AddProductCase(data, $"lte-stockquantity-{op}", nameof(Product.StockQuantity), op, "50", 2,
                products => products.All(p => p.StockQuantity <= 50).ShouldBeTrue());
        }

        AddProductCase(data, "string-contains", nameof(Product.Name), "contains", "Code", 1,
            products => products.Single().Name.ShouldContain("Code"));
        AddProductCase(data, "string-contains-case", nameof(Product.Name), "contains", "clean code", 0,
            products => products.Count.ShouldBe(0));
        AddProductCase(data, "string-startswith", nameof(Product.Name), "startswith", "Laptop", 1,
            products => products.Single().Name.ShouldStartWith("Laptop"));
        AddProductCase(data, "string-endswith", nameof(Product.Name), "endswith", "Headphones", 1,
            products => products.Single().Name.ShouldEndWith("Headphones"));

        AddProductCase(data, "in-numeric-comma", nameof(Product.StockQuantity), "in", "25,50", 2,
            products => products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]));
        AddProductCase(data, "in-string-pipe", nameof(Product.Name), "in", "Laptop Pro 15|Clean Code", 2,
            products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));
        AddProductCase(data, "in-nullable-weight", nameof(Product.Weight), "in", "null,0.25", 2,
            products =>
            {
                products.Count.ShouldBe(2);
                products.Count(p => p.Weight is null).ShouldBe(1);
                products.Any(p => p.Weight == 0.25m).ShouldBeTrue();
            });
        AddProductCase(data, "in-nullable-count", nameof(Product.Count), "in", "null,10", 2,
            products =>
            {
                products.Count.ShouldBe(2);
                products.Count(p => p.Count is null).ShouldBe(1);
                products.Any(p => p.Count == 10).ShouldBeTrue();
            });

        var productIdsCsv = $"{DataSeeder.productLaptopId},{DataSeeder.productHeadphonesId}";
        var productIdsPipe = $"{DataSeeder.productLaptopId}|{DataSeeder.productHeadphonesId}";
        var expectedProductIds = new[] { DataSeeder.productLaptopId, DataSeeder.productHeadphonesId }
            .OrderBy(x => x)
            .ToArray();
        AddProductCase(data, "in-guid-csv", nameof(Product.Id), "in", productIdsCsv, 2,
            products => products.Select(p => p.Id).OrderBy(x => x).ShouldBe(expectedProductIds));
        AddProductCase(data, "in-guid-pipe", nameof(Product.Id), "in", productIdsPipe, 2,
            products => products.Select(p => p.Id).OrderBy(x => x).ShouldBe(expectedProductIds));

        AddProductCase(data, "null-isnull", nameof(Product.Weight), "isnull", null, 1,
            products => products.All(p => p.Weight is null).ShouldBeTrue());
        AddProductCase(data, "null-notnull", nameof(Product.Weight), "notnull", null, 2,
            products => products.All(p => p.Weight is not null).ShouldBeTrue());
        AddProductCase(data, "store-notnull", nameof(Product.Store), "notnull", null, 3,
            products => products.Count.ShouldBe(3));
        AddProductCase(data, "store-isnull", nameof(Product.Store), "isnull", null, 0,
            products => products.Count.ShouldBe(0));

        AddProductCase(data, "dateonly-eq", nameof(Product.AddedIn), "eq", "2024-06-15", 1,
            products => products.Single().AddedIn.ShouldBe(new DateOnly(2024, 6, 15)));
        AddProductCase(data, "dateonly-gt", nameof(Product.AddedIn), "gt", "2024-07-01", 2,
            products => products.All(p => p.AddedIn > new DateOnly(2024, 7, 1)).ShouldBeTrue());
        AddProductCase(data, "timeonly-eq", nameof(Product.AddedAt), "eq", "10:30", 1,
            products => products.Single().AddedAt.ShouldBe(new TimeOnly(10, 30)));
        AddProductCase(data, "datetimeoffset-eq", nameof(Product.CreatedAt), "eq", "2024-06-01T00:00:00Z", 1,
            products => products.Single().CreatedAt.ShouldBe(
                DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture)));

        foreach (var op in GtOps)
        {
            AddProductCase(data, $"dto-gt-{op}", nameof(Product.CreatedAt), op, "2024-06-01T00:00:00Z", 2,
                products => products.All(p => p.CreatedAt > DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in GteOps)
        {
            AddProductCase(data, $"dto-gte-{op}", nameof(Product.CreatedAt), op, "2024-08-01T00:00:00Z", 2,
                products => products.All(p => p.CreatedAt >= DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in LtOps)
        {
            AddProductCase(data, $"dto-lt-{op}", nameof(Product.CreatedAt), op, "2024-08-01T00:00:00Z", 1,
                products => products.All(p => p.CreatedAt < DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in LteOps)
        {
            AddProductCase(data, $"dto-lte-{op}", nameof(Product.CreatedAt), op, "2024-08-01T00:00:00Z", 2,
                products => products.All(p => p.CreatedAt <= DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        var discontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        AddProductCase(data, "datetime-eq", nameof(Product.DiscontinuedAt), "eq", "2025-12-31T00:00:00Z", 3,
            products => products.All(p => p.DiscontinuedAt == discontinuedAt).ShouldBeTrue());

        AddProductCase(data, "timespan-eq", nameof(Product.FinishedAt), "eq", "1.00:00:00", 2,
            products => products.All(p => p.FinishedAt == TimeSpan.FromDays(1)).ShouldBeTrue());

        return data;
    }

    private static IReadOnlyDictionary<string, ReviewOperatorSpec> BuildReviewOperatorSpecs()
    {
        var data = new Dictionary<string, ReviewOperatorSpec>();

        foreach (var op in EqOps)
        {
            AddReviewCase(data, $"eq-rating-{op}", nameof(Review.Rating), op, "4", 1,
                reviews => reviews.Single().Rating.ShouldBe(4));
            AddReviewCase(data, $"eq-productid-{op}", nameof(Review.ProductId), op, DataSeeder.productLaptopId.ToString(), 1,
                reviews => reviews.Single().ProductId.ShouldBe(DataSeeder.productLaptopId));
            AddReviewCase(data, $"eq-comment-{op}", nameof(Review.Comment), op, "Great laptop, fast shipping.", 1,
                reviews =>
                {
                    var comment = reviews.Single().Comment;
                    comment.ShouldNotBeNull();
                    comment.ShouldBe("Great laptop, fast shipping.");
                });
        }

        foreach (var op in NeqOps)
        {
            AddReviewCase(data, $"neq-rating-{op}", nameof(Review.Rating), op, "4", 2,
                reviews => reviews.All(r => r.Rating != 4).ShouldBeTrue());
            AddReviewCase(data, $"neq-productid-{op}", nameof(Review.ProductId), op, DataSeeder.productLaptopId.ToString(), 2,
                reviews => reviews.Any(r => r.ProductId == DataSeeder.productLaptopId).ShouldBeFalse());
            AddReviewCase(data, $"neq-comment-{op}", nameof(Review.Comment), op, "Great laptop, fast shipping.", 2,
                reviews => reviews.Any(r => r.Comment == "Great laptop, fast shipping.").ShouldBeFalse());
        }

        foreach (var op in GtOps)
        {
            AddReviewCase(data, $"gt-rating-{op}", nameof(Review.Rating), op, "3", 2,
                reviews => reviews.All(r => r.Rating > 3).ShouldBeTrue());
        }

        foreach (var op in GteOps)
        {
            AddReviewCase(data, $"gte-rating-{op}", nameof(Review.Rating), op, "4", 2,
                reviews => reviews.All(r => r.Rating >= 4).ShouldBeTrue());
        }

        foreach (var op in LtOps)
        {
            AddReviewCase(data, $"lt-rating-{op}", nameof(Review.Rating), op, "4", 1,
                reviews => reviews.All(r => r.Rating < 4).ShouldBeTrue());
        }

        foreach (var op in LteOps)
        {
            AddReviewCase(data, $"lte-rating-{op}", nameof(Review.Rating), op, "4", 2,
                reviews => reviews.All(r => r.Rating <= 4).ShouldBeTrue());
        }

        AddReviewCase(data, "comment-contains", nameof(Review.Comment), "contains", "sound", 1,
            reviews =>
            {
                var comment = reviews.Single().Comment;
                comment.ShouldNotBeNull();
                comment.ShouldContain("sound");
            });
        AddReviewCase(data, "comment-startswith", nameof(Review.Comment), "startswith", "Great", 1,
            reviews =>
            {
                var comment = reviews.Single().Comment;
                comment.ShouldNotBeNull();
                comment.ShouldStartWith("Great");
            });
        AddReviewCase(data, "comment-endswith", nameof(Review.Comment), "endswith", "concepts.", 1,
            reviews =>
            {
                var comment = reviews.Single().Comment;
                comment.ShouldNotBeNull();
                comment.ShouldEndWith("concepts.");
            });

        AddReviewCase(data, "in-rating", nameof(Review.Rating), "in", "3,5", 2,
            reviews => reviews.Select(r => r.Rating).OrderBy(x => x).ShouldBe([3, 5]));

        var productIdsCsv = $"{DataSeeder.productLaptopId},{DataSeeder.productHeadphonesId}";
        var productIdsPipe = $"{DataSeeder.productLaptopId}|{DataSeeder.productHeadphonesId}";
        var expectedProductIds = new[] { DataSeeder.productLaptopId, DataSeeder.productHeadphonesId }
            .OrderBy(x => x)
            .ToArray();
        AddReviewCase(data, "in-productid-csv", nameof(Review.ProductId), "in", productIdsCsv, 2,
            reviews => reviews.Select(r => r.ProductId).OrderBy(x => x).ShouldBe(expectedProductIds));
        AddReviewCase(data, "in-productid-pipe", nameof(Review.ProductId), "in", productIdsPipe, 2,
            reviews => reviews.Select(r => r.ProductId).OrderBy(x => x).ShouldBe(expectedProductIds));

        AddReviewCase(data, "comment-isnull", nameof(Review.Comment), "isnull", null, 0,
            reviews => reviews.Count.ShouldBe(0));
        AddReviewCase(data, "comment-notnull", nameof(Review.Comment), "notnull", null, 3,
            reviews => reviews.All(r => r.Comment is not null).ShouldBeTrue());

        AddReviewCase(data, "dateonly-eq", nameof(Review.AddedIn), "eq", "2024-06-15", 1,
            reviews => reviews.Single().AddedIn.ShouldBe(new DateOnly(2024, 6, 15)));
        AddReviewCase(data, "dateonly-gt", nameof(Review.AddedIn), "gt", "2024-07-01", 2,
            reviews => reviews.All(r => r.AddedIn > new DateOnly(2024, 7, 1)).ShouldBeTrue());
        AddReviewCase(data, "timeonly-eq", nameof(Review.AddedAt), "eq", "10:30", 1,
            reviews => reviews.Single().AddedAt.ShouldBe(new TimeOnly(10, 30)));
        AddReviewCase(data, "datetimeoffset-eq", nameof(Review.CreatedAt), "eq", "2025-02-01T00:00:00Z", 1,
            reviews => reviews.Single().CreatedAt.ShouldBe(
                DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture)));

        foreach (var op in GtOps)
        {
            AddReviewCase(data, $"dto-gt-{op}", nameof(Review.CreatedAt), op, "2025-01-01T00:00:00Z", 2,
                reviews => reviews.All(r => r.CreatedAt > DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in GteOps)
        {
            AddReviewCase(data, $"dto-gte-{op}", nameof(Review.CreatedAt), op, "2025-02-01T00:00:00Z", 2,
                reviews => reviews.All(r => r.CreatedAt >= DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in LtOps)
        {
            AddReviewCase(data, $"dto-lt-{op}", nameof(Review.CreatedAt), op, "2025-03-01T00:00:00Z", 2,
                reviews => reviews.All(r => r.CreatedAt < DateTimeOffset.Parse("2025-03-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in LteOps)
        {
            AddReviewCase(data, $"dto-lte-{op}", nameof(Review.CreatedAt), op, "2025-02-01T00:00:00Z", 2,
                reviews => reviews.All(r => r.CreatedAt <= DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        var discontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        AddReviewCase(data, "datetime-eq", nameof(Review.DiscontinuedAt), "eq", "2025-12-31T00:00:00Z", 3,
            reviews => reviews.All(r => r.DiscontinuedAt == discontinuedAt).ShouldBeTrue());

        AddReviewCase(data, "timespan-eq", nameof(Review.FinishedAt), "eq", "1.00:00:00", 2,
            reviews => reviews.All(r => r.FinishedAt == TimeSpan.FromDays(1)).ShouldBeTrue());

        return data;
    }

    private static void AddProductCase(
        Dictionary<string, ProductOperatorSpec> data,
        string caseId,
        string property,
        string op,
        string? value,
        int expectedCount,
        Action<List<Product>> assert)
    {
        data[caseId] = new ProductOperatorSpec(new FilterClause(property, op, value), expectedCount, assert);
    }

    private static void AddReviewCase(
        Dictionary<string, ReviewOperatorSpec> data,
        string caseId,
        string property,
        string op,
        string? value,
        int expectedCount,
        Action<List<Review>> assert)
    {
        data[caseId] = new ReviewOperatorSpec(new FilterClause(property, op, value), expectedCount, assert);
    }
}
