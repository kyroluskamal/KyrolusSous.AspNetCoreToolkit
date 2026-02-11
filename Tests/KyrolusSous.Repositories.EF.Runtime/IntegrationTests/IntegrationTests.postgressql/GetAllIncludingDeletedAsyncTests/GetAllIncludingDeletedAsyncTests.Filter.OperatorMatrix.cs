namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    private static readonly string[] EqOps = ["eq", "=", "=="];
    private static readonly string[] NeqOps = ["neq", "!=", "<>"];
    private static readonly string[] GtOps = ["gt", ">"];
    private static readonly string[] GteOps = ["gte", ">="];
    private static readonly string[] LtOps = ["lt", "<"];
    private static readonly string[] LteOps = ["lte", "<="];

    public sealed record OperatorCase(
        KeyType KeyType,
        string Property,
        string Operator,
        string? Value,
        int ExpectedCount,
        Action<List<Product>>? AssertProducts,
        Action<List<Review>>? AssertReviews);

    public static TheoryData<OperatorCase> OperatorCases
    {
        get
        {
            var data = new TheoryData<OperatorCase>();
            AddNumericCases(data);
            AddStringCases(data);
            AddInCases(data);
            AddNullCases(data);
            AddDateCases(data);
            AddTimeSpanCases(data);
            return data;
        }
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports filter operators for single and composite keys")]
    [MemberData(nameof(OperatorCases))]
    public async Task GetAllIncludingDeletedAsync_Filter_Operators_Work(OperatorCase testCase)
    {
        var request = new QueryRequest(Filters: [new FilterClause(testCase.Property, testCase.Operator, testCase.Value)]);

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

    private static void AddNumericCases(TheoryData<OperatorCase> data)
    {
        foreach (var op in EqOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.StockQuantity), op, "50", 1,
                assertProducts: products => products.Single().StockQuantity.ShouldBe(50));
            AddCase(data, KeyType.Composite, nameof(Review.Rating), op, "4", 1,
                assertReviews: reviews => reviews.Single().Rating.ShouldBe(4));

            AddCase(data, KeyType.Single, nameof(Product.Price), op, "199", 1,
                assertProducts: products => products.Single().Price.ShouldBe(199m));

            AddCase(data, KeyType.Single, nameof(Product.IsActive), op, "false", 0,
                assertProducts: products => products.Count.ShouldBe(0));

            AddCase(data, KeyType.Single, nameof(Product.Id), op, DataSeeder.productLaptopId.ToString(), 1,
                assertProducts: products => products.Single().Id.ShouldBe(DataSeeder.productLaptopId));
            AddCase(data, KeyType.Composite, nameof(Review.ProductId), op, DataSeeder.productLaptopId.ToString(), 1,
                assertReviews: reviews => reviews.Single().ProductId.ShouldBe(DataSeeder.productLaptopId));

            AddCase(data, KeyType.Single, nameof(Product.Name), op, "Clean Code", 1,
                assertProducts: products => products.Single().Name.ShouldBe("Clean Code"));
            AddCase(data, KeyType.Composite, nameof(Review.Comment), op, "Great laptop, fast shipping.", 1,
                assertReviews: reviews =>
                {
                    var comment = reviews.Single().Comment;
                    comment.ShouldNotBeNull();
                    comment.ShouldBe("Great laptop, fast shipping.");
                });

            AddCase(data, KeyType.Single, nameof(Product.Weight), op, "null", 1,
                assertProducts: products =>
                {
                    products.Count.ShouldBe(1);
                    products.Single().Weight.ShouldBeNull();
                });
            AddCase(data, KeyType.Single, nameof(Product.Count), op, "null", 1,
                assertProducts: products =>
                {
                    products.Count.ShouldBe(1);
                    products.Single().Count.ShouldBeNull();
                });
        }

        foreach (var op in NeqOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.StockQuantity), op, "50", 2,
                assertProducts: products => products.All(p => p.StockQuantity != 50).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.Rating), op, "4", 2,
                assertReviews: reviews => reviews.All(r => r.Rating != 4).ShouldBeTrue());

            AddCase(data, KeyType.Single, nameof(Product.IsActive), op, "false", 3,
                assertProducts: products => products.All(p => p.IsActive).ShouldBeTrue());

            AddCase(data, KeyType.Single, nameof(Product.Id), op, DataSeeder.productLaptopId.ToString(), 2,
                assertProducts: products => products.Any(p => p.Id == DataSeeder.productLaptopId).ShouldBeFalse());
            AddCase(data, KeyType.Composite, nameof(Review.ProductId), op, DataSeeder.productLaptopId.ToString(), 2,
                assertReviews: reviews => reviews.Any(r => r.ProductId == DataSeeder.productLaptopId).ShouldBeFalse());

            AddCase(data, KeyType.Single, nameof(Product.Name), op, "Clean Code", 2,
                assertProducts: products => products.Any(p => p.Name == "Clean Code").ShouldBeFalse());
            AddCase(data, KeyType.Composite, nameof(Review.Comment), op, "Great laptop, fast shipping.", 2,
                assertReviews: reviews => reviews.Any(r => r.Comment == "Great laptop, fast shipping.").ShouldBeFalse());
        }

        foreach (var op in GtOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.StockQuantity), op, "25", 2,
                assertProducts: products => products.All(p => p.StockQuantity > 25).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.Rating), op, "3", 2,
                assertReviews: reviews => reviews.All(r => r.Rating > 3).ShouldBeTrue());
        }

        foreach (var op in GteOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.StockQuantity), op, "50", 2,
                assertProducts: products => products.All(p => p.StockQuantity >= 50).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.Rating), op, "4", 2,
                assertReviews: reviews => reviews.All(r => r.Rating >= 4).ShouldBeTrue());
        }

        foreach (var op in LtOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.StockQuantity), op, "50", 1,
                assertProducts: products => products.All(p => p.StockQuantity < 50).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.Rating), op, "4", 1,
                assertReviews: reviews => reviews.All(r => r.Rating < 4).ShouldBeTrue());
        }

        foreach (var op in LteOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.StockQuantity), op, "50", 2,
                assertProducts: products => products.All(p => p.StockQuantity <= 50).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.Rating), op, "4", 2,
                assertReviews: reviews => reviews.All(r => r.Rating <= 4).ShouldBeTrue());
        }
    }

    private static void AddStringCases(TheoryData<OperatorCase> data)
    {
        AddCase(data, KeyType.Single, nameof(Product.Name), "contains", "Code", 1,
            assertProducts: products => products.Single().Name.ShouldContain("Code"));
        AddCase(data, KeyType.Single, nameof(Product.Name), "contains", "clean code", 0,
            assertProducts: products => products.Count.ShouldBe(0));
        AddCase(data, KeyType.Single, nameof(Product.Name), "startswith", "Laptop", 1,
            assertProducts: products => products.Single().Name.ShouldStartWith("Laptop"));
        AddCase(data, KeyType.Single, nameof(Product.Name), "endswith", "Headphones", 1,
            assertProducts: products => products.Single().Name.ShouldEndWith("Headphones"));

        AddCase(data, KeyType.Composite, nameof(Review.Comment), "contains", "sound", 1,
            assertReviews: reviews =>
            {
                var comment = reviews.Single().Comment;
                comment.ShouldNotBeNull();
                comment.ShouldContain("sound");
            });
        AddCase(data, KeyType.Composite, nameof(Review.Comment), "startswith", "Great", 1,
            assertReviews: reviews =>
            {
                var comment = reviews.Single().Comment;
                comment.ShouldNotBeNull();
                comment.ShouldStartWith("Great");
            });
        AddCase(data, KeyType.Composite, nameof(Review.Comment), "endswith", "concepts.", 1,
            assertReviews: reviews =>
            {
                var comment = reviews.Single().Comment;
                comment.ShouldNotBeNull();
                comment.ShouldEndWith("concepts.");
            });
    }

    private static void AddInCases(TheoryData<OperatorCase> data)
    {
        var productIdsCsv = $"{DataSeeder.productLaptopId},{DataSeeder.productHeadphonesId}";
        var productIdsPipe = $"{DataSeeder.productLaptopId}|{DataSeeder.productHeadphonesId}";
        var expectedProductIds = new[] { DataSeeder.productLaptopId, DataSeeder.productHeadphonesId }
            .OrderBy(x => x)
            .ToArray();

        AddCase(data, KeyType.Single, nameof(Product.StockQuantity), "in", "25,50", 2,
            assertProducts: products => products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]));
        AddCase(data, KeyType.Composite, nameof(Review.Rating), "in", "3,5", 2,
            assertReviews: reviews => reviews.Select(r => r.Rating).OrderBy(x => x).ShouldBe([3, 5]));

        AddCase(data, KeyType.Single, nameof(Product.Name), "in", "Laptop Pro 15|Clean Code", 2,
            assertProducts: products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));

        AddCase(data, KeyType.Single, nameof(Product.Weight), "in", "null,0.25", 2,
            assertProducts: products =>
            {
                products.Count.ShouldBe(2);
                products.Count(p => p.Weight is null).ShouldBe(1);
                products.Any(p => p.Weight == 0.25m).ShouldBeTrue();
            });
        AddCase(data, KeyType.Single, nameof(Product.Count), "in", "null,10", 2,
            assertProducts: products =>
            {
                products.Count.ShouldBe(2);
                products.Count(p => p.Count is null).ShouldBe(1);
                products.Any(p => p.Count == 10).ShouldBeTrue();
            });

        AddCase(data, KeyType.Single, nameof(Product.Id), "in", productIdsCsv, 2,
            assertProducts: products => products.Select(p => p.Id).OrderBy(x => x).ShouldBe(expectedProductIds));
        AddCase(data, KeyType.Single, nameof(Product.Id), "in", productIdsPipe, 2,
            assertProducts: products => products.Select(p => p.Id).OrderBy(x => x).ShouldBe(expectedProductIds));

        AddCase(data, KeyType.Composite, nameof(Review.ProductId), "in", productIdsCsv, 2,
            assertReviews: reviews => reviews.Select(r => r.ProductId).OrderBy(x => x).ShouldBe(expectedProductIds));
        AddCase(data, KeyType.Composite, nameof(Review.ProductId), "in", productIdsPipe, 2,
            assertReviews: reviews => reviews.Select(r => r.ProductId).OrderBy(x => x).ShouldBe(expectedProductIds));
    }

    private static void AddNullCases(TheoryData<OperatorCase> data)
    {
        AddCase(data, KeyType.Single, nameof(Product.Weight), "isnull", null, 1,
            assertProducts: products => products.All(p => p.Weight is null).ShouldBeTrue());
        AddCase(data, KeyType.Single, nameof(Product.Weight), "notnull", null, 2,
            assertProducts: products => products.All(p => p.Weight is not null).ShouldBeTrue());

        AddCase(data, KeyType.Single, nameof(Product.Store), "notnull", null, 3,
            assertProducts: products => products.Count.ShouldBe(3));
        AddCase(data, KeyType.Single, nameof(Product.Store), "isnull", null, 0,
            assertProducts: products => products.Count.ShouldBe(0));

        AddCase(data, KeyType.Composite, nameof(Review.Comment), "isnull", null, 0,
            assertReviews: reviews => reviews.Count.ShouldBe(0));
        AddCase(data, KeyType.Composite, nameof(Review.Comment), "notnull", null, 3,
            assertReviews: reviews => reviews.All(r => r.Comment is not null).ShouldBeTrue());
    }

    private static void AddDateCases(TheoryData<OperatorCase> data)
    {
        AddCase(data, KeyType.Single, nameof(Product.AddedIn), "eq", "2024-06-15", 1,
            assertProducts: products => products.Single().AddedIn.ShouldBe(new DateOnly(2024, 6, 15)));
        AddCase(data, KeyType.Composite, nameof(Review.AddedIn), "eq", "2024-06-15", 1,
            assertReviews: reviews => reviews.Single().AddedIn.ShouldBe(new DateOnly(2024, 6, 15)));

        AddCase(data, KeyType.Single, nameof(Product.AddedIn), "gt", "2024-07-01", 2,
            assertProducts: products => products.All(p => p.AddedIn > new DateOnly(2024, 7, 1)).ShouldBeTrue());
        AddCase(data, KeyType.Composite, nameof(Review.AddedIn), "gt", "2024-07-01", 2,
            assertReviews: reviews => reviews.All(r => r.AddedIn > new DateOnly(2024, 7, 1)).ShouldBeTrue());

        AddCase(data, KeyType.Single, nameof(Product.AddedAt), "eq", "10:30", 1,
            assertProducts: products => products.Single().AddedAt.ShouldBe(new TimeOnly(10, 30)));
        AddCase(data, KeyType.Composite, nameof(Review.AddedAt), "eq", "10:30", 1,
            assertReviews: reviews => reviews.Single().AddedAt.ShouldBe(new TimeOnly(10, 30)));

        AddCase(data, KeyType.Single, nameof(Product.CreatedAt), "eq", "2024-06-01T00:00:00Z", 1,
            assertProducts: products => products.Single().CreatedAt.ShouldBe(
                DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture)));
        AddCase(data, KeyType.Composite, nameof(Review.CreatedAt), "eq", "2025-02-01T00:00:00Z", 1,
            assertReviews: reviews => reviews.Single().CreatedAt.ShouldBe(
                DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture)));

        foreach (var op in GtOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.CreatedAt), op, "2024-06-01T00:00:00Z", 2,
                assertProducts: products => products.All(p => p.CreatedAt > DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.CreatedAt), op, "2025-01-01T00:00:00Z", 2,
                assertReviews: reviews => reviews.All(r => r.CreatedAt > DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in GteOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.CreatedAt), op, "2024-08-01T00:00:00Z", 2,
                assertProducts: products => products.All(p => p.CreatedAt >= DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.CreatedAt), op, "2025-02-01T00:00:00Z", 2,
                assertReviews: reviews => reviews.All(r => r.CreatedAt >= DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in LtOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.CreatedAt), op, "2024-08-01T00:00:00Z", 1,
                assertProducts: products => products.All(p => p.CreatedAt < DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.CreatedAt), op, "2025-03-01T00:00:00Z", 2,
                assertReviews: reviews => reviews.All(r => r.CreatedAt < DateTimeOffset.Parse("2025-03-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        foreach (var op in LteOps)
        {
            AddCase(data, KeyType.Single, nameof(Product.CreatedAt), op, "2024-08-01T00:00:00Z", 2,
                assertProducts: products => products.All(p => p.CreatedAt <= DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
            AddCase(data, KeyType.Composite, nameof(Review.CreatedAt), op, "2025-02-01T00:00:00Z", 2,
                assertReviews: reviews => reviews.All(r => r.CreatedAt <= DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        }

        var discontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        AddCase(data, KeyType.Single, nameof(Product.DiscontinuedAt), "eq", "2025-12-31T00:00:00Z", 3,
            assertProducts: products => products.All(p => p.DiscontinuedAt == discontinuedAt).ShouldBeTrue());
        AddCase(data, KeyType.Composite, nameof(Review.DiscontinuedAt), "eq", "2025-12-31T00:00:00Z", 3,
            assertReviews: reviews => reviews.All(r => r.DiscontinuedAt == discontinuedAt).ShouldBeTrue());
    }

    private static void AddTimeSpanCases(TheoryData<OperatorCase> data)
    {
        AddCase(data, KeyType.Single, nameof(Product.FinishedAt), "eq", "1.00:00:00", 2,
            assertProducts: products => products.All(p => p.FinishedAt == TimeSpan.FromDays(1)).ShouldBeTrue());
        AddCase(data, KeyType.Composite, nameof(Review.FinishedAt), "eq", "1.00:00:00", 2,
            assertReviews: reviews => reviews.All(r => r.FinishedAt == TimeSpan.FromDays(1)).ShouldBeTrue());
    }
#pragma warning disable S107
    private static void AddCase(
        TheoryData<OperatorCase> data,
        KeyType keyType,
        string property,
        string op,
        string? value,
        int expectedCount,
        Action<List<Product>>? assertProducts = null,
        Action<List<Review>>? assertReviews = null)
        => data.Add(new OperatorCase(keyType, property, op, value, expectedCount, assertProducts, assertReviews));
#pragma warning restore S107
}
