namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    private sealed record IncludePolicySpec(
        KyrolusDefaultIncludeMode? Mode,
        List<string>? IncludeProperties,
        bool ExpectStore,
        int ExpectedReviewCount);

    private static readonly IReadOnlyDictionary<string, IncludePolicySpec> DefaultIncludeSpecs = BuildDefaultIncludeSpecs();
    private static readonly IReadOnlyDictionary<string, ByIdHttpSpec> IncludePropertySpecs = BuildIncludePropertySpecs();
    private static readonly IReadOnlyDictionary<string, ByIdHttpSpec> MultipleIncludeSpecs = BuildMultipleIncludeSpecs();
    private static readonly IReadOnlyDictionary<string, Func<GetByIdAsyncTests, Task>> IncludeGraphSpecs = BuildIncludeGraphSpecs();
    private static readonly IReadOnlyDictionary<string, Func<GetByIdAsyncTests, Task>> BlankIncludeSpecs = BuildBlankIncludeSpecs();
    private static readonly IReadOnlyDictionary<string, Func<GetByIdAsyncTests, Task>> IncludeExpressionSpecs = BuildIncludeExpressionSpecs();

    public static TheoryData<string> DefaultIncludeCases => CaseIdsFrom(DefaultIncludeSpecs);
    public static TheoryData<string> IncludePropertyCases => CaseIdsFrom(IncludePropertySpecs);
    public static TheoryData<string> MultipleIncludeCases => CaseIdsFrom(MultipleIncludeSpecs);
    public static TheoryData<string> IncludeGraphCases => CaseIdsFrom(IncludeGraphSpecs);
    public static TheoryData<string> BlankIncludeCases => CaseIdsFrom(BlankIncludeSpecs);
    public static TheoryData<string> IncludeExpressionCases => CaseIdsFrom(IncludeExpressionSpecs);

    [Theory(DisplayName = "GetByIdAsync applies default include policy")]
    [MemberData(nameof(DefaultIncludeCases))]
    public async Task GetByIdAsync_DefaultIncludes_Policy_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = DefaultIncludeSpecs[caseId];
        var policy = spec.Mode is null
            ? new KyrolusRepositoryPolicy().SetDefaultIncludeProperties<Product>("Store")
            : new KyrolusRepositoryPolicy { DefaultIncludeMode = spec.Mode.Value }
                .SetDefaultIncludeProperties<Product>("Store");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var product = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: spec.IncludeProperties,
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        product.ShouldNotBeNull();
        (product.Store is not null).ShouldBe(spec.ExpectStore);
        product.Reviews.Count.ShouldBe(spec.ExpectedReviewCount);
    }

    [Theory(DisplayName = "GetByIdAsync returns entity with Include Properties")]
    [MemberData(nameof(IncludePropertyCases))]
    public Task GetByIdAsync_IncludeProperties_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunByIdHttpCase(IncludePropertySpecs, caseId);
    }

    [Theory(DisplayName = "GetByIdAsync returns entity with multiple Includes")]
    [MemberData(nameof(MultipleIncludeCases))]
    public Task GetByIdAsync_MultipleIncludes_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunByIdHttpCase(MultipleIncludeSpecs, caseId);
    }

    [Theory(DisplayName = "GetByIdAsync supports Include Graphs with Include Properties")]
    [MemberData(nameof(IncludeGraphCases))]
    public Task GetByIdAsync_WithIncludeGraphs_IncludeProperties_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return IncludeGraphSpecs[caseId](this);
    }

    [Theory(DisplayName = "GetByIdAsync ignores blank include strings and still applies valid includes")]
    [MemberData(nameof(BlankIncludeCases))]
    public Task GetByIdAsync_BlankIncludeStrings_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return BlankIncludeSpecs[caseId](this);
    }

    [Theory(DisplayName = "GetByIdAsync returns entity with Include Expressions")]
    [MemberData(nameof(IncludeExpressionCases))]
    public Task GetByIdAsync_WithIncludeExpressions_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return IncludeExpressionSpecs[caseId](this);
    }

    private static IReadOnlyDictionary<string, IncludePolicySpec> BuildDefaultIncludeSpecs()
        => new Dictionary<string, IncludePolicySpec>
        {
            ["default"] = new IncludePolicySpec(
                Mode: null,
                IncludeProperties: null,
                ExpectStore: true,
                ExpectedReviewCount: 0),
            ["merge"] = new IncludePolicySpec(
                Mode: KyrolusDefaultIncludeMode.Merge,
                IncludeProperties: [nameof(Product.Reviews)],
                ExpectStore: true,
                ExpectedReviewCount: 1),
            ["replace"] = new IncludePolicySpec(
                Mode: KyrolusDefaultIncludeMode.Replace,
                IncludeProperties: [nameof(Product.Reviews)],
                ExpectStore: false,
                ExpectedReviewCount: 1)
        };

    private static IReadOnlyDictionary<string, ByIdHttpSpec> BuildIncludePropertySpecs()
        => new Dictionary<string, ByIdHttpSpec>
        {
            ["single"] = new ByIdHttpSpec(
                Kind: EntityKind.Product,
                SingleKey: productLaptopId,
                CompositeKeys: null,
                Request: new QueryRequest(Includes: ["Store"]),
                ExpectedStatus: HttpStatusCode.OK,
                AssertProduct: p => p.Store.ShouldNotBeNull(),
                AssertReview: null),

            ["composite"] = new ByIdHttpSpec(
                Kind: EntityKind.Review,
                SingleKey: null,
                CompositeKeys: CompositeKey_ProductReview,
                Request: new QueryRequest(Includes: ["Product", "Customer"]),
                ExpectedStatus: HttpStatusCode.OK,
                AssertProduct: null,
                AssertReview: r =>
                {
                    r.Product.ShouldNotBeNull();
                    r.Customer.ShouldNotBeNull();
                })
        };

    private static IReadOnlyDictionary<string, ByIdHttpSpec> BuildMultipleIncludeSpecs()
        => new Dictionary<string, ByIdHttpSpec>
        {
            ["single"] = new ByIdHttpSpec(
                Kind: EntityKind.Product,
                SingleKey: productHeadphonesId,
                CompositeKeys: null,
                Request: new QueryRequest(Includes: ["ProductCategories.Category", "OrderLines.Order"]),
                ExpectedStatus: HttpStatusCode.OK,
                AssertProduct: p =>
                {
                    p.ProductCategories.ShouldNotBeNull();
                    p.ProductCategories.First().Category.ShouldNotBeNull();
                    p.OrderLines.ShouldNotBeNull();
                    p.OrderLines.First().Order.ShouldNotBeNull();
                },
                AssertReview: null),

            ["composite"] = new ByIdHttpSpec(
                Kind: EntityKind.Review,
                SingleKey: null,
                CompositeKeys: CompositeKey_ProductReview,
                Request: new QueryRequest(Includes: ["Product.Store", "Customer.Store"]),
                ExpectedStatus: HttpStatusCode.OK,
                AssertProduct: null,
                AssertReview: r =>
                {
                    r.Product.ShouldNotBeNull();
                    r.Product.Store.ShouldNotBeNull();
                    r.Customer.ShouldNotBeNull();
                    r.Customer.Store.ShouldNotBeNull();
                })
        };

    private static IReadOnlyDictionary<string, Func<GetByIdAsyncTests, Task>> BuildIncludeGraphSpecs()
        => new Dictionary<string, Func<GetByIdAsyncTests, Task>>
        {
            ["single"] = async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

                var product = await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    includeProperties: ["Store", "", ""],
                    includeGraph: new IncludeGraph<Product>(x => x.Reviews),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);

                product.ShouldNotBeNull();
                product.Store.ShouldNotBeNull();
                product.Reviews.ShouldNotBeNull();
                product.Reviews.Count.ShouldBe(1);
            },
            ["composite"] = async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();

                var review = await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    includeProperties: ["Customer", "", ""],
                    includeGraph: new IncludeGraph<Review>(x => x.Product, x => x.Customer!.Store),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);

                review.ShouldNotBeNull();
                review.Product.ShouldNotBeNull();
                review.Customer.ShouldNotBeNull();
                review.Customer.Store.ShouldNotBeNull();
            }
        };

    private static IReadOnlyDictionary<string, Func<GetByIdAsyncTests, Task>> BuildBlankIncludeSpecs()
        => new Dictionary<string, Func<GetByIdAsyncTests, Task>>
        {
            ["single"] = async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

                counter.Reset();

                var product = await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    includeProperties: ["", "   ", "Reviews", "OrderLines", "ProductCategories"],
                    asNoTracking: true,
                    useSplitQuery: true,
                    cancellationToken: default);
                counter.Count.ShouldBe(4, $"Expected 4 SQL commands with split query and 3 collections, got {counter.Count}");
                product.ShouldNotBeNull();
            },
            ["composite"] = async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();
                var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
                counter.Reset();

                var review = await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    includeProperties: ["", "   ", "Customer"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: true,
                    cancellationToken: default);
                counter.Count.ShouldBe(1, $"Expected 1 SQL command and no collections with split query, got {counter.Count}");
                review.ShouldNotBeNull();
            }
        };

    private static IReadOnlyDictionary<string, Func<GetByIdAsyncTests, Task>> BuildIncludeExpressionSpecs()
        => new Dictionary<string, Func<GetByIdAsyncTests, Task>>
        {
            ["single"] = async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var product = await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default,
                    e => e.Reviews,
                    e => e.Store);
                product.ShouldNotBeNull();
                product.Store.ShouldNotBeNull();
                product.Reviews.ShouldNotBeNull();
                product.Reviews.Count.ShouldBe(1);
            },
            ["composite"] = async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();

                var review = await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default,
                    e => e.Product,
                    e => e.Customer!.Store);
                review.ShouldNotBeNull();
                review.Product.ShouldNotBeNull();
                review.Customer.ShouldNotBeNull();
                review.Customer.Store.ShouldNotBeNull();
            }
        };
}
