namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SaveChangesAsyncTests;

public partial class SaveChangesAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record SaveSuccessSpec(bool IsComposite);
    private static readonly IReadOnlyDictionary<string, SaveSuccessSpec> SuccessSpecs = BuildSuccessSpecs();
    public static TheoryData<string> SuccessCases => CaseIdsFrom(SuccessSpecs);

    [Theory(DisplayName = "SaveChangesAsync persists pending changes")]
    [MemberData(nameof(SuccessCases))]
    public async Task SaveChangesAsync_WithPendingChanges_Persists(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SuccessSpecs[caseId];

        if (spec.IsComposite)
        {
            var key = (ProductId: DataSeeder.productBookId, CustomerId: DataSeeder.customerJohnId);
            var review = CreateValidReview(key.ProductId, key.CustomerId, rating: 3, comment: $"save-composite-{Guid.NewGuid():N}");

            try
            {
                using var scope = Factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

                db.Reviews.Add(review);
                var affected = await uow.SaveChangesAsync();

                affected.ShouldBeGreaterThan(0);
                var persisted = await FindReviewAsync(key.ProductId, key.CustomerId);
                persisted.ShouldNotBeNull();
                persisted!.Comment.ShouldBe(review.Comment);
            }
            finally
            {
                await CleanupReviewAsync(key.ProductId, key.CustomerId);
            }

            return;
        }

        var id = Guid.NewGuid();
        var product = CreateValidProduct(id: id, name: $"save-single-{id:N}");

        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            db.Products.Add(product);
            var affected = await uow.SaveChangesAsync();

            affected.ShouldBeGreaterThan(0);
            var persisted = await FindProductAsync(id);
            persisted.ShouldNotBeNull();
            persisted!.Name.ShouldBe(product.Name);
        }
        finally
        {
            await CleanupProductAsync(id);
        }
    }

    [Fact(DisplayName = "SaveChangesAsync returns zero when there are no changes")]
    public async Task SaveChangesAsync_NoPendingChanges_ReturnsZero()
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var affected = await uow.SaveChangesAsync();
        affected.ShouldBe(0);
    }

    private static IReadOnlyDictionary<string, SaveSuccessSpec> BuildSuccessSpecs()
        => new Dictionary<string, SaveSuccessSpec>
        {
            ["single-key"] = new(IsComposite: false),
            ["composite-key"] = new(IsComposite: true)
        };
}
