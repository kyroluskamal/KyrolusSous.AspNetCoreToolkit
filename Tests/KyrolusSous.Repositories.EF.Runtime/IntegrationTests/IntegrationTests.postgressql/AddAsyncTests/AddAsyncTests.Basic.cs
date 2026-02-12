namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddAsyncTests;

public partial class AddAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record AddSingleKeySpec(Func<Product> CreateEntity, Action<Product> AssertCreated, Action<Product> AssertPersisted);
    private sealed record AddCompositeKeySpec(Func<Review> CreateEntity, Action<Review> AssertCreated, Action<Review> AssertPersisted);

    private static readonly IReadOnlyDictionary<string, AddSingleKeySpec> SingleKeySuccessSpecs = BuildSingleKeySuccessSpecs();
    private static readonly IReadOnlyDictionary<string, AddCompositeKeySpec> CompositeKeySuccessSpecs = BuildCompositeKeySuccessSpecs();

    public static TheoryData<string> SingleKeySuccessCases => CaseIdsFrom(SingleKeySuccessSpecs);
    public static TheoryData<string> CompositeKeySuccessCases => CaseIdsFrom(CompositeKeySuccessSpecs);

    [Theory(DisplayName = "AddAsync adds single-key entities")]
    [MemberData(nameof(SingleKeySuccessCases))]
    public async Task AddAsync_SingleKey_AddsEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleKeySuccessSpecs[caseId];
        var entity = spec.CreateEntity();

        try
        {
            var (response, content) = await PostEntityAsync<Product>(entity);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.Headers.Location.ShouldNotBeNull();
            response.Headers.Location!.ToString().ShouldContain("/api/product");

            var created = JsonSerializer.Deserialize<Product>(content, JsonOptions);
            created.ShouldNotBeNull();
            spec.AssertCreated(created!);

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == entity.Id);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(persisted!);
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Theory(DisplayName = "AddAsync adds composite-key entities")]
    [MemberData(nameof(CompositeKeySuccessCases))]
    public async Task AddAsync_CompositeKey_AddsEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeKeySuccessSpecs[caseId];
        var entity = spec.CreateEntity();

        try
        {
            var (response, content) = await PostEntityAsync<Review>(entity);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.Headers.Location.ShouldNotBeNull();
            response.Headers.Location!.ToString().ShouldContain("/api/review");

            var created = JsonSerializer.Deserialize<Review>(content, JsonOptions);
            created.ShouldNotBeNull();
            spec.AssertCreated(created!);

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.Reviews.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(persisted!);
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    private static IReadOnlyDictionary<string, AddSingleKeySpec> BuildSingleKeySuccessSpecs()
        => new Dictionary<string, AddSingleKeySpec>
        {
            ["minimal"] = new(
                CreateEntity: () => CreateValidProduct(
                    weight: 1.25m,
                    count: 7,
                    addedAt: null),
                AssertCreated: entity =>
                {
                    entity.Id.ShouldNotBe(Guid.Empty);
                    entity.Name.ShouldStartWith("Product-");
                    entity.IsDeleted.ShouldBeFalse();
                    entity.AddedAt.ShouldBeNull();
                },
                AssertPersisted: entity =>
                {
                    entity.Price.ShouldBe(79.99m);
                    entity.Weight.ShouldBe(1.25m);
                    entity.Count.ShouldBe(7);
                }),

            ["nullables"] = new(
                CreateEntity: () => CreateValidProduct(
                    price: 149.5m,
                    stockQuantity: 0,
                    weight: null,
                    count: null,
                    addedAt: new TimeOnly(16, 45)),
                AssertCreated: entity =>
                {
                    entity.StockQuantity.ShouldBe(0);
                    entity.Weight.ShouldBeNull();
                    entity.Count.ShouldBeNull();
                    entity.AddedAt.ShouldBe(new TimeOnly(16, 45));
                },
                AssertPersisted: entity =>
                {
                    entity.Price.ShouldBe(149.5m);
                    entity.Weight.ShouldBeNull();
                    entity.Count.ShouldBeNull();
                })
        };

    private static IReadOnlyDictionary<string, AddCompositeKeySpec> BuildCompositeKeySuccessSpecs()
        => new Dictionary<string, AddCompositeKeySpec>
        {
            ["basic"] = new(
                CreateEntity: () => CreateValidReview(
                    productId: DataSeeder.productLaptopId,
                    customerId: DataSeeder.customerJohnId,
                    rating: 2,
                    comment: "Average quality.",
                    addedAt: new TimeOnly(13, 20)),
                AssertCreated: entity =>
                {
                    entity.ProductId.ShouldBe(DataSeeder.productLaptopId);
                    entity.CustomerId.ShouldBe(DataSeeder.customerJohnId);
                    entity.Rating.ShouldBe(2);
                    entity.Comment.ShouldBe("Average quality.");
                    entity.IsDeleted.ShouldBeFalse();
                },
                AssertPersisted: entity =>
                {
                    entity.AddedAt.ShouldBe(new TimeOnly(13, 20));
                    entity.FinishedAt.ShouldBe(TimeSpan.FromHours(10));
                }),

            ["nullable-fields"] = new(
                CreateEntity: () => CreateValidReview(
                    productId: DataSeeder.productBookId,
                    customerId: DataSeeder.customerJohnId,
                    rating: 5,
                    comment: null,
                    addedAt: null),
                AssertCreated: entity =>
                {
                    entity.Comment.ShouldBeNull();
                    entity.AddedAt.ShouldBeNull();
                    entity.Rating.ShouldBe(5);
                },
                AssertPersisted: entity =>
                {
                    entity.Comment.ShouldBeNull();
                    entity.AddedAt.ShouldBeNull();
                    entity.ProductId.ShouldBe(DataSeeder.productBookId);
                })
        };
}

