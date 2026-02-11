namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    private static readonly string[] EqOps = ["eq", "=", "=="];
    private static readonly string[] NeqOps = ["neq", "!=", "<>"];
    private static readonly string[] GtOps = ["gt", ">"];
    private static readonly string[] GteOps = ["gte", ">="];
    private static readonly string[] LtOps = ["lt", "<"];
    private static readonly string[] LteOps = ["lte", "<="];

    private sealed record ProductFilterSpec(FilterClause Filter, int ExpectedCount, Action<List<Product>> Assert);
    private sealed record PaymentFilterSpec(FilterClause Filter, int ExpectedCount, Action<List<Payment>> Assert);

    private static readonly IReadOnlyDictionary<string, ProductFilterSpec> ProductFilterSpecs = BuildProductFilterSpecs();
    private static readonly IReadOnlyDictionary<string, PaymentFilterSpec> PaymentFilterSpecs = BuildPaymentFilterSpecs();

    public static TheoryData<string> ProductFilterCases => CaseIdsFrom(ProductFilterSpecs);
    public static TheoryData<string> PaymentFilterCases => CaseIdsFrom(PaymentFilterSpecs);

    [Theory(DisplayName = "GetAllAsync supports filter operators for products")]
    [MemberData(nameof(ProductFilterCases))]
    public async Task GetAllAsync_Filter_Operators_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = ProductFilterSpecs[caseId];
        var request = new QueryRequest(Filters: [spec.Filter]);
        var products = await GetOkListAsync<Product>(request);
        products.Count.ShouldBe(spec.ExpectedCount);
        spec.Assert(products);
    }

    [Theory(DisplayName = "GetAllAsync supports null operators for payments")]
    [MemberData(nameof(PaymentFilterCases))]
    public async Task GetAllAsync_Payment_NullOperators_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = PaymentFilterSpecs[caseId];
        var request = new QueryRequest(Filters: [spec.Filter]);
        var payments = await GetOkListAsync<Payment>(request);
        payments.Count.ShouldBe(spec.ExpectedCount);
        spec.Assert(payments);
    }

    private static IReadOnlyDictionary<string, ProductFilterSpec> BuildProductFilterSpecs()
    {
        var data = new Dictionary<string, ProductFilterSpec>();

        foreach (var op in EqOps)
        {
            AddProductCase(data, $"eq-numeric-{op}", new FilterClause(nameof(Product.StockQuantity), op, "25"), 1,
                products => products.Single().StockQuantity.ShouldBe(25));
        }

        foreach (var op in NeqOps)
        {
            AddProductCase(data, $"neq-numeric-{op}", new FilterClause(nameof(Product.StockQuantity), op, "25"), 2,
                products => products.All(p => p.StockQuantity != 25).ShouldBeTrue());
        }

        foreach (var op in GtOps)
        {
            AddProductCase(data, $"gt-numeric-{op}", new FilterClause(nameof(Product.StockQuantity), op, "25"), 2,
                products => products.All(p => p.StockQuantity > 25).ShouldBeTrue());
        }

        foreach (var op in GteOps)
        {
            AddProductCase(data, $"gte-numeric-{op}", new FilterClause(nameof(Product.StockQuantity), op, "50"), 2,
                products => products.All(p => p.StockQuantity >= 50).ShouldBeTrue());
        }

        foreach (var op in LtOps)
        {
            AddProductCase(data, $"lt-numeric-{op}", new FilterClause(nameof(Product.StockQuantity), op, "50"), 1,
                products => products.All(p => p.StockQuantity < 50).ShouldBeTrue());
        }

        foreach (var op in LteOps)
        {
            AddProductCase(data, $"lte-numeric-{op}", new FilterClause(nameof(Product.StockQuantity), op, "50"), 2,
                products => products.All(p => p.StockQuantity <= 50).ShouldBeTrue());
        }

        AddProductCase(data, "bool-eq", new FilterClause(nameof(Product.IsActive), "eq", "false"), 0,
            products => products.Count.ShouldBe(0));
        AddProductCase(data, "bool-neq", new FilterClause(nameof(Product.IsActive), "neq", "false"), 3,
            products => products.All(p => p.IsActive).ShouldBeTrue());

        AddProductCase(data, "guid-eq", new FilterClause(nameof(Product.Id), "eq", DataSeeder.productLaptopId.ToString()), 1,
            products => products.Single().Id.ShouldBe(DataSeeder.productLaptopId));

        AddProductCase(data, "string-eq", new FilterClause(nameof(Product.Name), "eq", "Clean Code"), 1,
            products => products.Single().Name.ShouldBe("Clean Code"));
        AddProductCase(data, "string-contains", new FilterClause(nameof(Product.Name), "contains", "Code"), 1,
            products => products.Single().Name.ShouldContain("Code"));
        AddProductCase(data, "string-contains-case", new FilterClause(nameof(Product.Name), "contains", "clean code"), 0,
            products => products.Count.ShouldBe(0));
        AddProductCase(data, "string-startswith", new FilterClause(nameof(Product.Name), "startswith", "Laptop"), 1,
            products => products.Single().Name.ShouldStartWith("Laptop"));
        AddProductCase(data, "string-endswith", new FilterClause(nameof(Product.Name), "endswith", "Headphones"), 1,
            products => products.Single().Name.ShouldEndWith("Headphones"));

        AddProductCase(data, "in-numeric-comma", new FilterClause(nameof(Product.StockQuantity), "in", "25,50"), 2,
            products => products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]));
        AddProductCase(data, "in-numeric-pipe", new FilterClause(nameof(Product.StockQuantity), "in", "25|50"), 2,
            products => products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]));
        AddProductCase(data, "in-string-pipe", new FilterClause(nameof(Product.Name), "in", "Laptop Pro 15|Clean Code"), 2,
            products => products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]));

        AddProductCase(data, "between-decimal-comma", new FilterClause(nameof(Product.Price), "between", "100,300"), 1,
            products => products.Single().Price.ShouldBe(199m));
        AddProductCase(data, "between-decimal-dots", new FilterClause(nameof(Product.Price), "between", "100..300"), 1,
            products => products.Single().Price.ShouldBe(199m));
        AddProductCase(data, "between-decimal-pipe", new FilterClause(nameof(Product.Price), "between", "100|300"), 1,
            products => products.Single().Price.ShouldBe(199m));

        AddProductCase(data, "any-collection", new FilterClause(nameof(Product.ProductCategories), "any", $"CategoryId = {DataSeeder.categoryElectronicsId}"), 2,
            products => products.Any(p => p.Name == "Clean Code").ShouldBeFalse());
        AddProductCase(data, "all-collection", new FilterClause(nameof(Product.ProductCategories), "all", $"CategoryId = {DataSeeder.categoryBooksId}"), 1,
            products => products.Single().Name.ShouldBe("Clean Code"));

        AddProductCase(data, "dateonly-eq", new FilterClause(nameof(Product.AddedIn), "eq", "2024-06-15"), 1,
            products => products.Single().AddedIn.ShouldBe(new DateOnly(2024, 6, 15)));
        AddProductCase(data, "dateonly-gt", new FilterClause(nameof(Product.AddedIn), "gt", "2024-07-01"), 2,
            products => products.All(p => p.AddedIn > new DateOnly(2024, 7, 1)).ShouldBeTrue());

        AddProductCase(data, "timeonly-eq", new FilterClause(nameof(Product.AddedAt), "eq", "10:30"), 1,
            products => products.Single().AddedAt.ShouldBe(new TimeOnly(10, 30)));

        AddProductCase(data, "datetimeoffset-eq", new FilterClause(nameof(Product.CreatedAt), "eq", "2024-06-01T00:00:00Z"), 1,
            products => products.Single().CreatedAt.ShouldBe(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture)));
        AddProductCase(data, "datetimeoffset-gt", new FilterClause(nameof(Product.CreatedAt), "gt", "2024-06-01T00:00:00Z"), 2,
            products => products.All(p => p.CreatedAt > DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture)).ShouldBeTrue());

        AddProductCase(data, "datetime-eq", new FilterClause(nameof(Product.DiscontinuedAt), "eq", "2025-12-31T00:00:00Z"), 3,
            products => products.All(p => p.DiscontinuedAt == new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)).ShouldBeTrue());

        AddProductCase(data, "nullable-isnull", new FilterClause(nameof(Product.Weight), "isnull", null), 1,
            products => products.All(p => p.Weight is null).ShouldBeTrue());
        AddProductCase(data, "nullable-notnull", new FilterClause(nameof(Product.Weight), "notnull", null), 2,
            products => products.All(p => p.Weight is not null).ShouldBeTrue());
        AddProductCase(data, "nullable-eq-null", new FilterClause(nameof(Product.Count), "eq", "null"), 1,
            products => products.Single().Count.ShouldBeNull());
        AddProductCase(data, "nullable-in", new FilterClause(nameof(Product.Count), "in", "null,10"), 2,
            products =>
            {
                products.Count(p => p.Count is null).ShouldBe(1);
                products.Any(p => p.Count == 10).ShouldBeTrue();
            });

        return data;
    }

    private static Dictionary<string, PaymentFilterSpec> BuildPaymentFilterSpecs()
    {
        return new Dictionary<string, PaymentFilterSpec>
        {
            ["paidat-isnull"] = new PaymentFilterSpec(
                    new FilterClause(nameof(Payment.PaidAt), "isnull", null),
                    0,
                    payments => payments.Count.ShouldBe(0)),
            ["paidat-notnull"] = new PaymentFilterSpec(
                    new FilterClause(nameof(Payment.PaidAt), "notnull", null),
                    1,
                    payments =>
                    {
                        payments.Count.ShouldBe(1);
                        payments[0].PaidAt.ShouldNotBeNull();
                    })
        };
    }


    private static void AddProductCase(
        Dictionary<string, ProductFilterSpec> data,
        string caseId,
        FilterClause filter,
        int expectedCount,
        Action<List<Product>> assert)
        => data[caseId] = new ProductFilterSpec(filter, expectedCount, assert);

    // CaseIdsFrom is defined in GetAllAsyncTests.Helpers.cs
}
