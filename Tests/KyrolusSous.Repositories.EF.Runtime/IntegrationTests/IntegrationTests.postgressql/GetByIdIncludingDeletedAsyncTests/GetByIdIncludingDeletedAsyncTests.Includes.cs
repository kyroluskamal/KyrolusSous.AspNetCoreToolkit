namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    private sealed record IncludeSpec(Func<GetByIdIncludingDeletedAsyncTests, Task> Run);

    private static readonly IReadOnlyDictionary<string, IncludeSpec> IncludeSpecs = BuildIncludeSpecs();

    public static TheoryData<string> IncludeCases => CaseIdsFrom(IncludeSpecs);

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync supports include options")]
    [MemberData(nameof(IncludeCases))]
    public Task GetByIdIncludingDeletedAsync_Includes_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return IncludeSpecs[caseId].Run(this);
    }

    private static IReadOnlyDictionary<string, IncludeSpec> BuildIncludeSpecs()
        => new Dictionary<string, IncludeSpec>
        {
            ["single-include-properties"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var product = await repo.GetByIdIncludingDeletedAsync(
                    ExistingProductId,
                    includeProperties: ["Store"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
                product.ShouldNotBeNull();
                product.Store.ShouldNotBeNull();
            }),
            ["composite-include-properties"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var review = await repo.GetByIdIncludingDeletedAsync(
                    ExistingReviewKey,
                    includeProperties: ["Product", "Customer"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
                review.ShouldNotBeNull();
                review.Product.ShouldNotBeNull();
                review.Customer.ShouldNotBeNull();
            }),
            ["single-include-graph"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var product = await repo.GetByIdIncludingDeletedAsync(
                    ExistingProductId,
                    includeProperties: ["Store"],
                    includeGraph: new IncludeGraph<Product>(x => x.Reviews),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
                product.ShouldNotBeNull();
                product.Store.ShouldNotBeNull();
                product.Reviews.Count.ShouldBe(1);
            }),
            ["composite-include-graph"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var review = await repo.GetByIdIncludingDeletedAsync(
                    ExistingReviewKey,
                    includeProperties: ["Customer"],
                    includeGraph: new IncludeGraph<Review>(x => x.Product, x => x.Customer!.Store),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
                review.ShouldNotBeNull();
                review.Product.ShouldNotBeNull();
                review.Customer.ShouldNotBeNull();
                review.Customer.Store.ShouldNotBeNull();
            }),
            ["single-include-expressions"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var product = await repo.GetByIdIncludingDeletedAsync(
                    ExistingProductId,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default,
                    e => e.Reviews,
                    e => e.Store);
                product.ShouldNotBeNull();
                product.Reviews.Count.ShouldBe(1);
                product.Store.ShouldNotBeNull();
            }),
            ["composite-include-expressions"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var review = await repo.GetByIdIncludingDeletedAsync(
                    ExistingReviewKey,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default,
                    e => e.Product,
                    e => e.Customer!.Store);
                review.ShouldNotBeNull();
                review.Product.ShouldNotBeNull();
                review.Customer.ShouldNotBeNull();
                review.Customer.Store.ShouldNotBeNull();
            }),
            ["single-blank-includes"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
                counter.Reset();

                var product = await repo.GetByIdIncludingDeletedAsync(
                    ExistingProductId,
                    includeProperties: ["", "   ", "Reviews", "OrderLines", "ProductCategories"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: true,
                    cancellationToken: default);
                product.ShouldNotBeNull();
                counter.Count.ShouldBe(4);
            }),
            ["composite-blank-includes"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
                counter.Reset();

                var review = await repo.GetByIdIncludingDeletedAsync(
                    ExistingReviewKey,
                    includeProperties: ["", "   ", "Customer"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: true,
                    cancellationToken: default);
                review.ShouldNotBeNull();
                counter.Count.ShouldBe(1);
            }),
            ["single-default-includes-merge"] = new(async test =>
            {
                var policy = new KyrolusRepositoryPolicy { DefaultIncludeMode = KyrolusDefaultIncludeMode.Merge }
                    .SetDefaultIncludeProperties<Product>("Store");
                var customFactory = test.WithPolicy(policy);
                using var scope = customFactory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

                var product = await repo.GetByIdIncludingDeletedAsync(
                    ExistingProductId,
                    includeProperties: ["Reviews"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);

                product.ShouldNotBeNull();
                product.Store.ShouldNotBeNull();
                product.Reviews.Count.ShouldBe(1);
            }),
            ["single-default-includes-replace"] = new(async test =>
            {
                var policy = new KyrolusRepositoryPolicy { DefaultIncludeMode = KyrolusDefaultIncludeMode.Replace }
                    .SetDefaultIncludeProperties<Product>("Store");
                var customFactory = test.WithPolicy(policy);
                using var scope = customFactory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

                var product = await repo.GetByIdIncludingDeletedAsync(
                    ExistingProductId,
                    includeProperties: ["Reviews"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);

                product.ShouldNotBeNull();
                product.Store.ShouldBeNull();
                product.Reviews.Count.ShouldBe(1);
            })
        };
}
