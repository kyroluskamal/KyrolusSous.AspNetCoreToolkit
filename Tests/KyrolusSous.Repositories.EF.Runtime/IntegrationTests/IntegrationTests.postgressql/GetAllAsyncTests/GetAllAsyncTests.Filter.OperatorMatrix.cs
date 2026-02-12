namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    private static readonly string[] EqOps = ["eq", "=", "=="];
    private static readonly string[] NeqOps = ["neq", "!=", "<>"];
    private static readonly string[] GtOps = ["gt", ">"];
    private static readonly string[] GteOps = ["gte", ">="];
    private static readonly string[] LtOps = ["lt", "<"];
    private static readonly string[] LteOps = ["lte", "<="];

    private sealed record OperatorSpec<TEntity>(FilterClause Filter, int ExpectedCount, Action<List<TEntity>> Assert);

    private static readonly IReadOnlyDictionary<string, OperatorSpec<Product>> SingleKeyOperatorSpecs = BuildSingleKeyOperatorSpecs();
    private static readonly IReadOnlyDictionary<string, OperatorSpec<Review>> CompositeKeyOperatorSpecs = BuildCompositeKeyOperatorSpecs();

    public static TheoryData<string> SingleKeyOperatorCases => CaseIdsFrom(SingleKeyOperatorSpecs);
    public static TheoryData<string> CompositeKeyOperatorCases => CaseIdsFrom(CompositeKeyOperatorSpecs);

    [Theory(DisplayName = "GetAllAsync supports filter operators for single-key entities")]
    [MemberData(nameof(SingleKeyOperatorCases))]
    public async Task GetAllAsync_SingleKey_Operators_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleKeyOperatorSpecs[caseId];
        var request = new QueryRequest(Filters: [spec.Filter]);
        var products = await GetOkListAsync<Product>(request);
        products.Count.ShouldBe(spec.ExpectedCount);
        spec.Assert(products);
    }

    [Theory(DisplayName = "GetAllAsync supports filter operators for composite-key entities")]
    [MemberData(nameof(CompositeKeyOperatorCases))]
    public async Task GetAllAsync_CompositeKey_Operators_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeKeyOperatorSpecs[caseId];
        var request = new QueryRequest(Filters: [spec.Filter]);
        var reviews = await GetOkListAsync<Review>(request);
        reviews.Count.ShouldBe(spec.ExpectedCount);
        spec.Assert(reviews);
    }

    private static IReadOnlyDictionary<string, OperatorSpec<Product>> BuildSingleKeyOperatorSpecs()
    {
        var data = new Dictionary<string, OperatorSpec<Product>>();

        AddEqCases(data, "stockquantity", nameof(Product.StockQuantity), "50", 1,
            products => products.Single().StockQuantity.ShouldBe(50));
        AddNeqCases(data, "stockquantity", nameof(Product.StockQuantity), "50", 2,
            products => products.All(p => p.StockQuantity != 50).ShouldBeTrue());
        AddGtCases(data, "stockquantity", nameof(Product.StockQuantity), "25", 2,
            products => products.All(p => p.StockQuantity > 25).ShouldBeTrue());
        AddGteCases(data, "stockquantity", nameof(Product.StockQuantity), "50", 2,
            products => products.All(p => p.StockQuantity >= 50).ShouldBeTrue());
        AddLtCases(data, "stockquantity", nameof(Product.StockQuantity), "50", 1,
            products => products.All(p => p.StockQuantity < 50).ShouldBeTrue());
        AddLteCases(data, "stockquantity", nameof(Product.StockQuantity), "50", 2,
            products => products.All(p => p.StockQuantity <= 50).ShouldBeTrue());

        AddEqCases(data, "price", nameof(Product.Price), "199", 1,
            products => products.Single().Price.ShouldBe(199m));
        AddNeqCases(data, "price", nameof(Product.Price), "199", 2,
            products => products.All(p => p.Price != 199m).ShouldBeTrue());
        AddGtCases(data, "price", nameof(Product.Price), "100", 2,
            products => products.All(p => p.Price > 100m).ShouldBeTrue());
        AddGteCases(data, "price", nameof(Product.Price), "199", 2,
            products => products.All(p => p.Price >= 199m).ShouldBeTrue());
        AddLtCases(data, "price", nameof(Product.Price), "100", 1,
            products => products.All(p => p.Price < 100m).ShouldBeTrue());
        AddLteCases(data, "price", nameof(Product.Price), "199", 2,
            products => products.All(p => p.Price <= 199m).ShouldBeTrue());

        AddEqCases(data, "isactive", nameof(Product.IsActive), "true", 3,
            products => products.All(p => p.IsActive).ShouldBeTrue());
        AddNeqCases(data, "isactive", nameof(Product.IsActive), "false", 3,
            products => products.All(p => p.IsActive).ShouldBeTrue());
        AddCase(data, "in-isactive-comma", nameof(Product.IsActive), "in", "true,false", 3,
            products => products.Count.ShouldBe(3));
        AddCase(data, "in-isactive-pipe", nameof(Product.IsActive), "in", "true|false", 3,
            products => products.Count.ShouldBe(3));

        AddEqCases(data, "id", nameof(Product.Id), DataSeeder.productLaptopId.ToString(), 1,
            products => products.Single().Id.ShouldBe(DataSeeder.productLaptopId));
        AddNeqCases(data, "id", nameof(Product.Id), DataSeeder.productLaptopId.ToString(), 2,
            products => products.Any(p => p.Id == DataSeeder.productLaptopId).ShouldBeFalse());

        AddEqCases(data, "name", nameof(Product.Name), "Clean Code", 1,
            products => products.Single().Name.ShouldBe("Clean Code"));
        AddNeqCases(data, "name", nameof(Product.Name), "Clean Code", 2,
            products => products.Any(p => p.Name == "Clean Code").ShouldBeFalse());
        AddCase(data, "name-contains", nameof(Product.Name), "contains", "Code", 1,
            products => products.Single().Name.ShouldContain("Code"));
        AddCase(data, "name-contains-case", nameof(Product.Name), "contains", "clean code", 0,
            products => products.Count.ShouldBe(0));
        AddCase(data, "name-startswith", nameof(Product.Name), "startswith", "Laptop", 1,
            products => products.Single().Name.ShouldStartWith("Laptop"));
        AddCase(data, "name-endswith", nameof(Product.Name), "endswith", "Headphones", 1,
            products => products.Single().Name.ShouldEndWith("Headphones"));

        AddEqCases(data, "dateonly", nameof(Product.AddedIn), "2024-06-15", 1,
            products => products.Single().AddedIn.ShouldBe(new DateOnly(2024, 6, 15)));
        AddGtCases(data, "dateonly", nameof(Product.AddedIn), "2024-07-01", 2,
            products => products.All(p => p.AddedIn > new DateOnly(2024, 7, 1)).ShouldBeTrue());
        AddGteCases(data, "dateonly", nameof(Product.AddedIn), "2024-08-05", 2,
            products => products.All(p => p.AddedIn >= new DateOnly(2024, 8, 5)).ShouldBeTrue());
        AddLtCases(data, "dateonly", nameof(Product.AddedIn), "2024-08-05", 1,
            products => products.All(p => p.AddedIn < new DateOnly(2024, 8, 5)).ShouldBeTrue());
        AddLteCases(data, "dateonly", nameof(Product.AddedIn), "2024-08-05", 2,
            products => products.All(p => p.AddedIn <= new DateOnly(2024, 8, 5)).ShouldBeTrue());

        AddEqCases(data, "timeonly", nameof(Product.AddedAt), "10:30", 1,
            products => products.Single().AddedAt.ShouldBe(new TimeOnly(10, 30)));
        AddGtCases(data, "timeonly", nameof(Product.AddedAt), "10:30", 1,
            products => products.All(p => p.AddedAt > new TimeOnly(10, 30)).ShouldBeTrue());
        AddGteCases(data, "timeonly", nameof(Product.AddedAt), "10:30", 2,
            products => products.All(p => p.AddedAt >= new TimeOnly(10, 30)).ShouldBeTrue());
        AddLtCases(data, "timeonly", nameof(Product.AddedAt), "10:30", 1,
            products => products.All(p => p.AddedAt < new TimeOnly(10, 30)).ShouldBeTrue());
        AddLteCases(data, "timeonly", nameof(Product.AddedAt), "10:30", 2,
            products => products.All(p => p.AddedAt <= new TimeOnly(10, 30)).ShouldBeTrue());

        var createdAtJun = DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture);
        var createdAtAug = DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture);
        AddEqCases(data, "datetimeoffset", nameof(Product.CreatedAt), "2024-06-01T00:00:00Z", 1,
            products => products.Single().CreatedAt.ShouldBe(createdAtJun));
        AddGtCases(data, "datetimeoffset", nameof(Product.CreatedAt), "2024-06-01T00:00:00Z", 2,
            products => products.All(p => p.CreatedAt > createdAtJun).ShouldBeTrue());
        AddGteCases(data, "datetimeoffset", nameof(Product.CreatedAt), "2024-08-01T00:00:00Z", 2,
            products => products.All(p => p.CreatedAt >= createdAtAug).ShouldBeTrue());
        AddLtCases(data, "datetimeoffset", nameof(Product.CreatedAt), "2024-08-01T00:00:00Z", 1,
            products => products.All(p => p.CreatedAt < createdAtAug).ShouldBeTrue());
        AddLteCases(data, "datetimeoffset", nameof(Product.CreatedAt), "2024-08-01T00:00:00Z", 2,
            products => products.All(p => p.CreatedAt <= createdAtAug).ShouldBeTrue());

        var discontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        AddEqCases(data, "datetime", nameof(Product.DiscontinuedAt), "2025-12-31T00:00:00Z", 3,
            products => products.All(p => p.DiscontinuedAt == discontinuedAt).ShouldBeTrue());
        AddGtCases(data, "datetime", nameof(Product.DiscontinuedAt), "2025-12-30T00:00:00Z", 3,
            products => products.All(p => p.DiscontinuedAt > new DateTime(2025, 12, 30, 0, 0, 0, DateTimeKind.Utc)).ShouldBeTrue());
        AddGteCases(data, "datetime", nameof(Product.DiscontinuedAt), "2025-12-31T00:00:00Z", 3,
            products => products.All(p => p.DiscontinuedAt >= discontinuedAt).ShouldBeTrue());
        AddLtCases(data, "datetime", nameof(Product.DiscontinuedAt), "2026-01-01T00:00:00Z", 3,
            products => products.All(p => p.DiscontinuedAt < new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).ShouldBeTrue());
        AddLteCases(data, "datetime", nameof(Product.DiscontinuedAt), "2025-12-31T00:00:00Z", 3,
            products => products.All(p => p.DiscontinuedAt <= discontinuedAt).ShouldBeTrue());

        AddEqCases(data, "timespan", nameof(Product.FinishedAt), "1.00:00:00", 2,
            products => products.All(p => p.FinishedAt == TimeSpan.FromDays(1)).ShouldBeTrue());

        AddCase(data, "isnull-weight", nameof(Product.Weight), "isnull", null, 1,
            products => products.All(p => p.Weight is null).ShouldBeTrue());
        AddCase(data, "notnull-weight", nameof(Product.Weight), "notnull", null, 2,
            products => products.All(p => p.Weight is not null).ShouldBeTrue());
        AddCase(data, "isnull-count", nameof(Product.Count), "isnull", null, 1,
            products => products.All(p => p.Count is null).ShouldBeTrue());
        AddCase(data, "notnull-count", nameof(Product.Count), "notnull", null, 2,
            products => products.All(p => p.Count is not null).ShouldBeTrue());
        AddCase(data, "eq-weight-null-literal", nameof(Product.Weight), "eq", "null", 1,
            products => products.Single().Weight.ShouldBeNull());
        AddCase(data, "eq-weight-null-value", nameof(Product.Weight), "eq", null, 1,
            products => products.Single().Weight.ShouldBeNull());
        AddCase(data, "eq-count-null-literal", nameof(Product.Count), "eq", "null", 1,
            products => products.Single().Count.ShouldBeNull());
        AddCase(data, "eq-count-null-value", nameof(Product.Count), "eq", null, 1,
            products => products.Single().Count.ShouldBeNull());

        AddCase(data, "in-stockquantity-comma", nameof(Product.StockQuantity), "in", "25,50", 2,
            products => products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]));
        AddCase(data, "in-stockquantity-pipe", nameof(Product.StockQuantity), "in", "25|50", 2,
            products => products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]));
        AddCase(data, "in-stockquantity-quoted", nameof(Product.StockQuantity), "in", "'25','50'", 2,
            products => products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]));

        AddCase(data, "in-name-comma", nameof(Product.Name), "in", "Laptop Pro 15, Clean Code", 2,
            products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));
        AddCase(data, "in-name-pipe", nameof(Product.Name), "in", "Laptop Pro 15|Clean Code", 2,
            products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));
        AddCase(data, "in-name-quoted", nameof(Product.Name), "in", "\"Laptop Pro 15\"|\"Clean Code\"", 2,
            products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));
        AddCase(data, "in-name-escaped", nameof(Product.Name), "in", "\"Laptop\\ Pro 15\"|\"Clean Code\"", 2,
            products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));

        var productIdsCsv = $"{DataSeeder.productLaptopId},{DataSeeder.productHeadphonesId}";
        var productIdsPipe = $"{DataSeeder.productLaptopId}|{DataSeeder.productHeadphonesId}";
        var productIdsQuoted = $"\"{DataSeeder.productLaptopId}\",\"{DataSeeder.productHeadphonesId}\"";
        var expectedProductIds = new[] { DataSeeder.productLaptopId, DataSeeder.productHeadphonesId }
            .OrderBy(x => x)
            .ToArray();
        AddCase(data, "in-id-comma", nameof(Product.Id), "in", productIdsCsv, 2,
            products => products.Select(p => p.Id).OrderBy(x => x).ShouldBe(expectedProductIds));
        AddCase(data, "in-id-pipe", nameof(Product.Id), "in", productIdsPipe, 2,
            products => products.Select(p => p.Id).OrderBy(x => x).ShouldBe(expectedProductIds));
        AddCase(data, "in-id-quoted", nameof(Product.Id), "in", productIdsQuoted, 2,
            products => products.Select(p => p.Id).OrderBy(x => x).ShouldBe(expectedProductIds));

        AddCase(data, "in-addedin", nameof(Product.AddedIn), "in", "2024-06-15,2025-01-01", 2,
            products => products.Select(p => p.AddedIn).OrderBy(x => x).ShouldBe([new DateOnly(2024, 6, 15), new DateOnly(2025, 1, 1)]));
        AddCase(data, "in-addedat", nameof(Product.AddedAt), "in", "09:00,10:30", 2,
            products => products.Select(p => p.AddedAt).OrderBy(x => x).ShouldBe([new TimeOnly(9, 0), new TimeOnly(10, 30)]));
        AddCase(data, "in-createdat", nameof(Product.CreatedAt), "in", "2024-06-01T00:00:00Z|2025-01-01T00:00:00Z", 2,
            products => products.Select(p => p.CreatedAt).OrderBy(x => x).ShouldBe([
                DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture)
            ]));
        AddCase(data, "in-discontinuedat", nameof(Product.DiscontinuedAt), "in", "2025-12-31T00:00:00Z", 3,
            products => products.All(p => p.DiscontinuedAt == discontinuedAt).ShouldBeTrue());
        AddCase(data, "in-finishedat", nameof(Product.FinishedAt), "in", "1.00:00:00,2.00:00:00", 3,
            products => products.Select(p => p.FinishedAt).OrderBy(x => x).ShouldBe([TimeSpan.FromDays(1), TimeSpan.FromDays(1), TimeSpan.FromDays(2)]));
        AddCase(data, "in-weight-null", nameof(Product.Weight), "in", "null,0.25", 2,
            products =>
            {
                products.Count(p => p.Weight is null).ShouldBe(1);
                products.Any(p => p.Weight == 0.25m).ShouldBeTrue();
            });
        AddCase(data, "in-count-null", nameof(Product.Count), "in", "null,10", 2,
            products =>
            {
                products.Count(p => p.Count is null).ShouldBe(1);
                products.Any(p => p.Count == 10).ShouldBeTrue();
            });

        AddCase(data, "between-price-dots", nameof(Product.Price), "between", "100..300", 1,
            products => products.Single().Price.ShouldBe(199m));
        AddCase(data, "between-price-comma", nameof(Product.Price), "between", "100,300", 1,
            products => products.Single().Price.ShouldBe(199m));
        AddCase(data, "between-price-pipe", nameof(Product.Price), "between", "100|300", 1,
            products => products.Single().Price.ShouldBe(199m));
        AddCase(data, "between-price-quoted", nameof(Product.Price), "between", "\"100\"..\"300\"", 1,
            products => products.Single().Price.ShouldBe(199m));

        AddCase(data, "between-dateonly-dots", nameof(Product.AddedIn), "between", "2024-06-01..2024-12-31", 2,
            products => products.All(p => p.AddedIn >= new DateOnly(2024, 6, 1) && p.AddedIn <= new DateOnly(2024, 12, 31)).ShouldBeTrue());
        AddCase(data, "between-dateonly-comma", nameof(Product.AddedIn), "between", "2024-06-01,2024-12-31", 2,
            products => products.All(p => p.AddedIn >= new DateOnly(2024, 6, 1) && p.AddedIn <= new DateOnly(2024, 12, 31)).ShouldBeTrue());
        AddCase(data, "between-dateonly-pipe", nameof(Product.AddedIn), "between", "2024-06-01|2024-12-31", 2,
            products => products.All(p => p.AddedIn >= new DateOnly(2024, 6, 1) && p.AddedIn <= new DateOnly(2024, 12, 31)).ShouldBeTrue());

        AddCase(data, "between-timeonly-dots", nameof(Product.AddedAt), "between", "09:00..11:00", 2,
            products => products.All(p => p.AddedAt >= new TimeOnly(9, 0) && p.AddedAt <= new TimeOnly(11, 0)).ShouldBeTrue());
        AddCase(data, "between-timeonly-quoted", nameof(Product.AddedAt), "between", "'09:00'..'11:00'", 2,
            products => products.All(p => p.AddedAt >= new TimeOnly(9, 0) && p.AddedAt <= new TimeOnly(11, 0)).ShouldBeTrue());

        AddCase(data, "between-datetimeoffset-dots", nameof(Product.CreatedAt), "between", "2024-06-01T00:00:00Z..2024-12-31T00:00:00Z", 2,
            products => products.All(p => p.CreatedAt.Year == 2024).ShouldBeTrue());
        AddCase(data, "between-datetimeoffset-quoted", nameof(Product.CreatedAt), "between", "\"2024-06-01T00:00:00Z\"..\"2024-12-31T00:00:00Z\"", 2,
            products => products.All(p => p.CreatedAt.Year == 2024).ShouldBeTrue());

        AddCase(data, "between-datetime-dots", nameof(Product.DiscontinuedAt), "between", "2025-12-31T00:00:00Z..2025-12-31T00:00:00Z", 3,
            products => products.All(p => p.DiscontinuedAt == discontinuedAt).ShouldBeTrue());

        AddCase(data, "between-timespan-dots", nameof(Product.FinishedAt), "between", "1.00:00:00..2.00:00:00", 3,
            products => products.All(p => p.FinishedAt >= TimeSpan.FromDays(1) && p.FinishedAt <= TimeSpan.FromDays(2)).ShouldBeTrue());
        AddCase(data, "between-timespan-pipe", nameof(Product.FinishedAt), "between", "1.00:00:00|2.00:00:00", 3,
            products => products.All(p => p.FinishedAt >= TimeSpan.FromDays(1) && p.FinishedAt <= TimeSpan.FromDays(2)).ShouldBeTrue());

        AddCase(data, "any-productcategories", nameof(Product.ProductCategories), "any", $"CategoryId = {DataSeeder.categoryElectronicsId}", 2,
            products => products.Any(p => p.Name == "Clean Code").ShouldBeFalse());
        AddCase(data, "all-productcategories", nameof(Product.ProductCategories), "all", $"CategoryId = {DataSeeder.categoryBooksId}", 1,
            products => products.Single().Name.ShouldBe("Clean Code"));
        AddCase(data, "any-reviews", nameof(Product.Reviews), "any", "Rating >= 4", 2,
            products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));
        AddCase(data, "all-reviews", nameof(Product.Reviews), "all", "Rating >= 4", 2,
            products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));

        AddCase(data, "store-notnull", nameof(Product.Store), "notnull", null, 3,
            products => products.Count.ShouldBe(3));
        AddCase(data, "store-isnull", nameof(Product.Store), "isnull", null, 0,
            products => products.Count.ShouldBe(0));

        return data;
    }
    private static IReadOnlyDictionary<string, OperatorSpec<Review>> BuildCompositeKeyOperatorSpecs()
    {
        var data = new Dictionary<string, OperatorSpec<Review>>();

        AddEqCases(data, "rating", nameof(Review.Rating), "4", 1,
            reviews => reviews.Single().Rating.ShouldBe(4));
        AddNeqCases(data, "rating", nameof(Review.Rating), "4", 2,
            reviews => reviews.All(r => r.Rating != 4).ShouldBeTrue());
        AddGtCases(data, "rating", nameof(Review.Rating), "3", 2,
            reviews => reviews.All(r => r.Rating > 3).ShouldBeTrue());
        AddGteCases(data, "rating", nameof(Review.Rating), "4", 2,
            reviews => reviews.All(r => r.Rating >= 4).ShouldBeTrue());
        AddLtCases(data, "rating", nameof(Review.Rating), "4", 1,
            reviews => reviews.All(r => r.Rating < 4).ShouldBeTrue());
        AddLteCases(data, "rating", nameof(Review.Rating), "4", 2,
            reviews => reviews.All(r => r.Rating <= 4).ShouldBeTrue());

        AddEqCases(data, "isdeleted", nameof(Review.IsDeleted), "false", 3,
            reviews => reviews.All(r => !r.IsDeleted ).ShouldBeTrue());
        AddNeqCases(data, "isdeleted", nameof(Review.IsDeleted), "true", 3,
            reviews => reviews.All(r => !r.IsDeleted).ShouldBeTrue());
        AddCase(data, "in-isdeleted", nameof(Review.IsDeleted), "in", "true,false", 3,
            reviews => reviews.Count.ShouldBe(3));

        AddEqCases(data, "productid", nameof(Review.ProductId), DataSeeder.productLaptopId.ToString(), 1,
            reviews => reviews.Single().ProductId.ShouldBe(DataSeeder.productLaptopId));
        AddNeqCases(data, "productid", nameof(Review.ProductId), DataSeeder.productLaptopId.ToString(), 2,
            reviews => reviews.Any(r => r.ProductId == DataSeeder.productLaptopId).ShouldBeFalse());

        AddEqCases(data, "comment", nameof(Review.Comment), "Great laptop, fast shipping.", 1,
            reviews => reviews.Single().Comment.ShouldBe("Great laptop, fast shipping."));
        AddNeqCases(data, "comment", nameof(Review.Comment), "Great laptop, fast shipping.", 2,
            reviews => reviews.Any(r => r.Comment == "Great laptop, fast shipping.").ShouldBeFalse());
        AddCase(data, "comment-contains", nameof(Review.Comment), "contains", "sound", 1,
            reviews => reviews.Single().Comment!.ShouldContain("sound"));
        AddCase(data, "comment-startswith", nameof(Review.Comment), "startswith", "Great", 1,
            reviews => reviews.Single().Comment!.ShouldStartWith("Great"));
        AddCase(data, "comment-endswith", nameof(Review.Comment), "endswith", "concepts.", 1,
            reviews => reviews.Single().Comment!.ShouldEndWith("concepts."));

        AddEqCases(data, "dateonly", nameof(Review.AddedIn), "2024-06-15", 1,
            reviews => reviews.Single().AddedIn.ShouldBe(new DateOnly(2024, 6, 15)));
        AddGtCases(data, "dateonly", nameof(Review.AddedIn), "2024-07-01", 2,
            reviews => reviews.All(r => r.AddedIn > new DateOnly(2024, 7, 1)).ShouldBeTrue());
        AddGteCases(data, "dateonly", nameof(Review.AddedIn), "2024-08-05", 2,
            reviews => reviews.All(r => r.AddedIn >= new DateOnly(2024, 8, 5)).ShouldBeTrue());
        AddLtCases(data, "dateonly", nameof(Review.AddedIn), "2024-08-05", 1,
            reviews => reviews.All(r => r.AddedIn < new DateOnly(2024, 8, 5)).ShouldBeTrue());
        AddLteCases(data, "dateonly", nameof(Review.AddedIn), "2024-08-05", 2,
            reviews => reviews.All(r => r.AddedIn <= new DateOnly(2024, 8, 5)).ShouldBeTrue());

        AddEqCases(data, "timeonly", nameof(Review.AddedAt), "10:30", 1,
            reviews => reviews.Single().AddedAt.ShouldBe(new TimeOnly(10, 30)));
        AddGtCases(data, "timeonly", nameof(Review.AddedAt), "10:30", 1,
            reviews => reviews.All(r => r.AddedAt > new TimeOnly(10, 30)).ShouldBeTrue());
        AddGteCases(data, "timeonly", nameof(Review.AddedAt), "10:30", 2,
            reviews => reviews.All(r => r.AddedAt >= new TimeOnly(10, 30)).ShouldBeTrue());
        AddLtCases(data, "timeonly", nameof(Review.AddedAt), "10:30", 1,
            reviews => reviews.All(r => r.AddedAt < new TimeOnly(10, 30)).ShouldBeTrue());
        AddLteCases(data, "timeonly", nameof(Review.AddedAt), "10:30", 2,
            reviews => reviews.All(r => r.AddedAt <= new TimeOnly(10, 30)).ShouldBeTrue());

        var createdAtJan = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        var createdAtFeb = DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture);
        AddEqCases(data, "datetimeoffset", nameof(Review.CreatedAt), "2025-02-01T00:00:00Z", 1,
            reviews => reviews.Single().CreatedAt.ShouldBe(createdAtFeb));
        AddGtCases(data, "datetimeoffset", nameof(Review.CreatedAt), "2025-01-01T00:00:00Z", 2,
            reviews => reviews.All(r => r.CreatedAt > createdAtJan).ShouldBeTrue());
        AddGteCases(data, "datetimeoffset", nameof(Review.CreatedAt), "2025-02-01T00:00:00Z", 2,
            reviews => reviews.All(r => r.CreatedAt >= createdAtFeb).ShouldBeTrue());
        AddLtCases(data, "datetimeoffset", nameof(Review.CreatedAt), "2025-03-01T00:00:00Z", 2,
            reviews => reviews.All(r => r.CreatedAt < DateTimeOffset.Parse("2025-03-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        AddLteCases(data, "datetimeoffset", nameof(Review.CreatedAt), "2025-02-01T00:00:00Z", 2,
            reviews => reviews.All(r => r.CreatedAt <= createdAtFeb).ShouldBeTrue());

        var discontinuedAt = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        AddEqCases(data, "datetime", nameof(Review.DiscontinuedAt), "2025-12-31T00:00:00Z", 3,
            reviews => reviews.All(r => r.DiscontinuedAt == discontinuedAt).ShouldBeTrue());

        AddEqCases(data, "timespan", nameof(Review.FinishedAt), "1.00:00:00", 2,
            reviews => reviews.All(r => r.FinishedAt == TimeSpan.FromDays(1)).ShouldBeTrue());

        AddCase(data, "comment-isnull", nameof(Review.Comment), "isnull", null, 0,
            reviews => reviews.Count.ShouldBe(0));
        AddCase(data, "comment-notnull", nameof(Review.Comment), "notnull", null, 3,
            reviews => reviews.All(r => r.Comment is not null).ShouldBeTrue());
        AddCase(data, "deletedat-isnull", nameof(Review.DeletedAt), "isnull", null, 3,
            reviews => reviews.All(r => r.DeletedAt is null).ShouldBeTrue());
        AddCase(data, "deletedat-notnull", nameof(Review.DeletedAt), "notnull", null, 0,
            reviews => reviews.Count.ShouldBe(0));
        AddCase(data, "deletedat-eq-null-literal", nameof(Review.DeletedAt), "eq", "null", 3,
            reviews => reviews.All(r => r.DeletedAt is null).ShouldBeTrue());
        AddCase(data, "deletedat-eq-null-value", nameof(Review.DeletedAt), "eq", null, 3,
            reviews => reviews.All(r => r.DeletedAt is null).ShouldBeTrue());

        AddCase(data, "in-rating", nameof(Review.Rating), "in", "3,5", 2,
            reviews => reviews.Select(r => r.Rating).OrderBy(x => x).ShouldBe([3, 5]));
        var productIdsCsv = $"{DataSeeder.productLaptopId},{DataSeeder.productHeadphonesId}";
        var productIdsPipe = $"{DataSeeder.productLaptopId}|{DataSeeder.productHeadphonesId}";
        var expectedProductIds = new[] { DataSeeder.productLaptopId, DataSeeder.productHeadphonesId }
            .OrderBy(x => x)
            .ToArray();
        AddCase(data, "in-productid-csv", nameof(Review.ProductId), "in", productIdsCsv, 2,
            reviews => reviews.Select(r => r.ProductId).OrderBy(x => x).ShouldBe(expectedProductIds));
        AddCase(data, "in-productid-pipe", nameof(Review.ProductId), "in", productIdsPipe, 2,
            reviews => reviews.Select(r => r.ProductId).OrderBy(x => x).ShouldBe(expectedProductIds));
        AddCase(data, "in-customerid", nameof(Review.CustomerId), "in", DataSeeder.customerJaneId.ToString(), 2,
            reviews => reviews.All(r => r.CustomerId == DataSeeder.customerJaneId).ShouldBeTrue());

        AddCase(data, "between-rating", nameof(Review.Rating), "between", "4..5", 2,
            reviews => reviews.All(r => r.Rating >= 4 && r.Rating <= 5).ShouldBeTrue());
        AddCase(data, "between-dateonly", nameof(Review.AddedIn), "between", "2024-06-01..2024-12-31", 2,
            reviews => reviews.All(r => r.AddedIn >= new DateOnly(2024, 6, 1) && r.AddedIn <= new DateOnly(2024, 12, 31)).ShouldBeTrue());
        AddCase(data, "between-timeonly", nameof(Review.AddedAt), "between", "09:00..11:00", 2,
            reviews => reviews.All(r => r.AddedAt >= new TimeOnly(9, 0) && r.AddedAt <= new TimeOnly(11, 0)).ShouldBeTrue());
        AddCase(data, "between-datetimeoffset", nameof(Review.CreatedAt), "between", "2025-01-01T00:00:00Z..2025-02-01T00:00:00Z", 2,
            reviews => reviews.All(r => r.CreatedAt <= DateTimeOffset.Parse("2025-02-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());
        AddCase(data, "between-datetime", nameof(Review.DiscontinuedAt), "between", "2025-12-31T00:00:00Z..2025-12-31T00:00:00Z", 3,
            reviews => reviews.All(r => r.DiscontinuedAt == discontinuedAt).ShouldBeTrue());
        AddCase(data, "between-timespan", nameof(Review.FinishedAt), "between", "1.00:00:00..2.00:00:00", 3,
            reviews => reviews.All(r => r.FinishedAt >= TimeSpan.FromDays(1) && r.FinishedAt <= TimeSpan.FromDays(2)).ShouldBeTrue());

        return data;
    }
    private static void AddCase<TEntity>(
        Dictionary<string, OperatorSpec<TEntity>> data,
        string caseId,
        string property,
        string op,
        string? value,
        int expectedCount,
        Action<List<TEntity>> assert)
        => data[caseId] = new OperatorSpec<TEntity>(new FilterClause(property, op, value), expectedCount, assert);
    private static void AddEqCases<TEntity>(
        Dictionary<string, OperatorSpec<TEntity>> data,
        string label,
        string property,
        string? value,
        int expectedCount,
        Action<List<TEntity>> assert)
    {
        foreach (var op in EqOps)
            AddCase(data, $"eq-{label}-{op}", property, op, value, expectedCount, assert);
    }
    private static void AddNeqCases<TEntity>(
        Dictionary<string, OperatorSpec<TEntity>> data,
        string label,
        string property,
        string? value,
        int expectedCount,
        Action<List<TEntity>> assert)
    {
        foreach (var op in NeqOps)
            AddCase(data, $"neq-{label}-{op}", property, op, value, expectedCount, assert);
    }
    private static void AddGtCases<TEntity>(
        Dictionary<string, OperatorSpec<TEntity>> data,
        string label,
        string property,
        string value,
        int expectedCount,
        Action<List<TEntity>> assert)
    {
        foreach (var op in GtOps)
            AddCase(data, $"gt-{label}-{op}", property, op, value, expectedCount, assert);
    }
    private static void AddGteCases<TEntity>(
        Dictionary<string, OperatorSpec<TEntity>> data,
        string label,
        string property,
        string value,
        int expectedCount,
        Action<List<TEntity>> assert)
    {
        foreach (var op in GteOps)
            AddCase(data, $"gte-{label}-{op}", property, op, value, expectedCount, assert);
    }
    private static void AddLtCases<TEntity>(
        Dictionary<string, OperatorSpec<TEntity>> data,
        string label,
        string property,
        string value,
        int expectedCount,
        Action<List<TEntity>> assert)
    {
        foreach (var op in LtOps)
            AddCase(data, $"lt-{label}-{op}", property, op, value, expectedCount, assert);
    }
    private static void AddLteCases<TEntity>(
        Dictionary<string, OperatorSpec<TEntity>> data,
        string label,
        string property,
        string value,
        int expectedCount,
        Action<List<TEntity>> assert)
    {
        foreach (var op in LteOps)
            AddCase(data, $"lte-{label}-{op}", property, op, value, expectedCount, assert);
    }
}
