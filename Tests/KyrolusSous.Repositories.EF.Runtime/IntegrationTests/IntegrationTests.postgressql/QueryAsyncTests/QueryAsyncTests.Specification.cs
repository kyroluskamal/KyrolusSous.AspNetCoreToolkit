namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.QueryAsyncTests;

public partial class QueryAsyncTests
{
    [Fact(DisplayName = "QueryAsync specification returns projected single-key results")]
    public async Task QueryAsync_Specification_SingleKey_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var spec = new TestQuerySpecification<Product, ProductQueryProjection>
        {
            Filter = x => x.Price >= 199m,
            Selector = x => new ProductQueryProjection(x.Id, x.Sku, x.Price, x.Reviews.Count),
            OrderBy = q => q.OrderBy(x => x.Price),
            AsNoTracking = true,
            UseSplitQuery = false
        };

        var items = await repo.QueryAsync(spec);
        items.Select(x => x.Sku).ShouldBe(["NC-100", "LP-15"]);
    }

    [Fact(DisplayName = "QueryAsync specification returns projected composite-key results")]
    public async Task QueryAsync_Specification_CompositeKey_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var spec = new TestQuerySpecification<Review, ReviewQueryProjection>
        {
            Filter = x => x.Rating >= 4,
            Selector = x => new ReviewQueryProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
            OrderBy = q => q.OrderByDescending(x => x.Rating),
            AsNoTracking = true,
            UseSplitQuery = false
        };

        var items = await repo.QueryAsync(spec);
        items.Select(x => x.Rating).ShouldBe([5, 4]);
    }

    [Theory(DisplayName = "QueryAsync specification IncludeDeleted controls visibility of soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Specification_IncludeDeleted_Works(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var review = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "query-spec-soft-composite");
            await SeedReviewAsync(review);
            try
            {
                await SoftDeleteReviewAsync(review.ProductId, review.CustomerId);
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

                var excludedSpec = new TestQuerySpecification<Review, Review>
                {
                    Filter = x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId,
                    Selector = x => x,
                    IncludeDeleted = false,
                    AsNoTracking = true,
                    UseSplitQuery = false
                };

                var includedSpec = new TestQuerySpecification<Review, Review>
                {
                    Filter = x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId,
                    Selector = x => x,
                    IncludeDeleted = true,
                    AsNoTracking = true,
                    UseSplitQuery = false
                };

                (await repo.QueryAsync(excludedSpec)).ShouldBeEmpty();
                (await repo.QueryAsync(includedSpec)).Count.ShouldBe(1);
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "query-spec-soft-single");
        await SeedProductAsync(product);
        try
        {
            await SoftDeleteProductAsync(product.Id);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            var excludedSpec = new TestQuerySpecification<Product, Product>
            {
                Filter = x => x.Id == product.Id,
                Selector = x => x,
                IncludeDeleted = false,
                AsNoTracking = true,
                UseSplitQuery = false
            };

            var includedSpec = new TestQuerySpecification<Product, Product>
            {
                Filter = x => x.Id == product.Id,
                Selector = x => x,
                IncludeDeleted = true,
                AsNoTracking = true,
                UseSplitQuery = false
            };

            (await repo.QueryAsync(excludedSpec)).ShouldBeEmpty();
            (await repo.QueryAsync(includedSpec)).Count.ShouldBe(1);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Fact(DisplayName = "QueryAsync specification AsNoTracking is applied")]
    public async Task QueryAsync_Specification_AsNoTracking_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        db.ChangeTracker.Clear();
        var trackedSpec = new TestQuerySpecification<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            AsNoTracking = false,
            UseSplitQuery = false
        };
        _ = await repo.QueryAsync(trackedSpec);
        db.ChangeTracker.Entries().ShouldNotBeEmpty();

        db.ChangeTracker.Clear();
        var noTrackingSpec = new TestQuerySpecification<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            AsNoTracking = true,
            UseSplitQuery = false
        };
        _ = await repo.QueryAsync(noTrackingSpec);
        db.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "QueryAsync specification UseSplitQuery from IKyrolusHasSplitQuery is applied")]
    public async Task QueryAsync_Specification_UseSplitQuery_Interface_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var nonSplitSpec = new TestQuerySpecification<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            Includes = [x => x.Reviews, x => x.OrderLines, x => x.ProductCategories],
            AsNoTracking = true,
            UseSplitQuery = false
        };
        counter.Reset();
        _ = await repo.QueryAsync(nonSplitSpec);
        var nonSplitCommands = counter.Count;

        var splitSpec = new TestQuerySpecification<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            Includes = [x => x.Reviews, x => x.OrderLines, x => x.ProductCategories],
            AsNoTracking = true,
            UseSplitQuery = true
        };
        counter.Reset();
        _ = await repo.QueryAsync(splitSpec);
        var splitCommands = counter.Count;

        nonSplitCommands.ShouldBeGreaterThan(0);
        splitCommands.ShouldBeGreaterThan(nonSplitCommands);
    }

    [Fact(DisplayName = "QueryAsync specification without IKyrolusHasSplitQuery ignores policy split default")]
    public async Task QueryAsync_Specification_WithoutSplitInterface_IgnoresPolicySplitDefault()
    {
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = true };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var spec = new TestQuerySpecificationWithoutSplit<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            Includes = [x => x.Reviews, x => x.OrderLines, x => x.ProductCategories],
            AsNoTracking = true
        };

        counter.Reset();
        _ = await repo.QueryAsync(spec);
        var commandCount = counter.Count;

        commandCount.ShouldBe(1);
    }
}
