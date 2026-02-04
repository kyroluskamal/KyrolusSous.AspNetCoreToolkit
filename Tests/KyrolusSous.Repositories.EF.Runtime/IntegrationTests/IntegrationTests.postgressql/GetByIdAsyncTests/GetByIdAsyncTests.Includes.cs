namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    [Fact(DisplayName = "GetByIdAsync applies default include properties from policy when no explicit includes are provided")]
    public async Task GetByIdAsync_DefaultIncludes_Applied()
    {
        var policy = new KyrolusRepositoryPolicy()
            .SetDefaultIncludeProperties<Product>("Store");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var product = await repo.GetByIdAsync(Guid.Parse(productLaptopId), asNoTracking: true);
        product.ShouldNotBeNull();
        product.Store.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync merges default includes with explicit includes when mode is Merge")]
    public async Task GetByIdAsync_DefaultIncludes_Merge_Works()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultIncludeMode = KyrolusDefaultIncludeMode.Merge
        }.SetDefaultIncludeProperties<Product>("Store");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var product = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        product.ShouldNotBeNull();
        product.Store.ShouldNotBeNull();
        product.Reviews.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync ignores default includes when mode is Replace and explicit includes exist")]
    public async Task GetByIdAsync_DefaultIncludes_Replace_Works()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultIncludeMode = KyrolusDefaultIncludeMode.Replace
        }.SetDefaultIncludeProperties<Product>("Store");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var product = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        product.ShouldNotBeNull();
        product.Store.ShouldBeNull();
        product.Reviews.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with Include Properties with single key")]
    public async Task GetByIdAsync_IncludeProperties_SingleKey()
    {
        var (_, product, _) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, string>(productLaptopId,
            new QueryRequest(Includes: ["Store"]));
        product.ShouldNotBeNull();
        product.Store.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with Include Properties with composite key")]
    public async Task GetByIdAsync_IncludeProperties_CompositeKey()
    {
        var (_, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(CompositeKey_ProductReview,
            new QueryRequest(Includes: ["Product", "Customer"]));
        review.ShouldNotBeNull();
        review.Product.ShouldNotBeNull();
        review.Customer.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with multiple Includes - single key")]
    public async Task GetByIdAsync_MultipleIncludes_SingleKey()
    {
        var (_, product, _) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, string>(productHeadphonesId,
            new QueryRequest(Includes: ["ProductCategories.Category", "OrderLines.Order"]));
        product.ShouldNotBeNull();
        product.ProductCategories.ShouldNotBeNull();
        product.ProductCategories.First().Category.ShouldNotBeNull();
        product.OrderLines.ShouldNotBeNull();
        product.OrderLines.First().Order.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with multiple Includes - composite key")]
    public async Task GetByIdAsync_MultipleIncludes_CompositeKey()
    {
        var (_, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(CompositeKey_ProductReview,
            new QueryRequest(Includes: ["Product.Store", "Customer.Store"]));
        review.ShouldNotBeNull();
        review.Product.ShouldNotBeNull();
        review.Product.Store.ShouldNotBeNull();
        review.Customer.ShouldNotBeNull();
        review.Customer.Store.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with Include Graphs and Include Properties - single key")]
    public async Task GetByIdAsync_WithIncludeGraphs_IncludeProperties_SingleKey()
    {
        using var scope = Factory.Services.CreateScope();
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
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with Include Graphs and Include Properties - composite key")]
    public async Task GetByIdAsync_WithIncludeGraphs_IncludeProperties_CompositeKey()
    {
        using var scope = Factory.Services.CreateScope();
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

    [Fact(DisplayName = "GetByIdAsync ignores blank include strings and still applies valid includes - single key")]
    public async Task GetByIdAsync_BlankIncludeStrings_SingleKey()
    {
        using var scope = Factory.Services.CreateScope();
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
    }

    [Fact(DisplayName = "GetByIdAsync ignores blank include strings and still applies valid includes - composite key")]
    public async Task GetByIdAsync_BlankIncludeStrings_CompositeKey()
    {
        using var scope = Factory.Services.CreateScope();
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
    [Fact(DisplayName = "GetByIdAsync returns entity with Include Expressions - single key")]
    public async Task GetByIdAsync_WithIncludeExpressions_SingleKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            asNoTracking: true, useSplitQuery: null, cancellationToken: default,
            e => e.Reviews, e => e.Store);
        product.ShouldNotBeNull();
        product.Store.ShouldNotBeNull();
        product.Reviews.ShouldNotBeNull();
        product.Reviews.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with Include Expressions - composite key")]
    public async Task GetByIdAsync_WithIncludeExpressions_CompositeKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();

        var review = await repo.GetByIdAsync(
            CompositeKey_ProductReview, asNoTracking: true, useSplitQuery: null,
            cancellationToken: default, e => e.Product, e => e.Customer!.Store);
        review.ShouldNotBeNull();
        review.Product.ShouldNotBeNull();
        review.Customer.ShouldNotBeNull();
        review.Customer.Store.ShouldNotBeNull();
    }
}
