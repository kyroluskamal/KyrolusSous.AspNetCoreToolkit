namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SaveChangesWithRetryAsyncTests;

public partial class SaveChangesWithRetryAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record SuccessSpec(
        bool IsComposite,
        Func<Guid> BuildId);

    private static readonly IReadOnlyDictionary<string, SuccessSpec> SuccessSpecs = BuildSuccessSpecs();
    public static TheoryData<string> SuccessCases => CaseIdsFrom(SuccessSpecs);

    [Theory(DisplayName = "SaveChangesWithRetryAsync returns success for valid pending changes")]
    [MemberData(nameof(SuccessCases))]
    public async Task SaveChangesWithRetryAsync_Success_ReturnsSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SuccessSpecs[caseId];

        if (spec.IsComposite)
        {
            var customerId = DataSeeder.customerJohnId;
            var productId = DataSeeder.productBookId;
            var entity = CreateValidReview(productId, customerId, rating: 3, comment: $"uow-success-c-{Guid.NewGuid():N}");

            try
            {
                using var scope = Factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

                db.Reviews.Add(entity);
                var result = await uow.SaveChangesWithRetryAsync();

                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeGreaterThan(0);
                result.Concurrency.ShouldBeNull();

                (await FindReviewAsync(productId, customerId)).ShouldNotBeNull();
            }
            finally
            {
                await CleanupReviewAsync(productId, customerId);
            }

            return;
        }

        var id = spec.BuildId();
        var product = CreateValidProduct(id: id, name: $"uow-success-s-{id:N}");

        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            db.Products.Add(product);
            var result = await uow.SaveChangesWithRetryAsync(rowVersionPropertyName: "RowVersion");

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeGreaterThan(0);
            result.Concurrency.ShouldBeNull();

            (await FindProductAsync(id)).ShouldNotBeNull();
        }
        finally
        {
            await CleanupProductAsync(id);
        }
    }

    [Fact(DisplayName = "SaveChangesWithRetryAsync returns success with zero when no pending changes")]
    public async Task SaveChangesWithRetryAsync_NoChanges_ReturnsSuccessWithZero()
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var result = await uow.SaveChangesWithRetryAsync();

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        result.Exception.ShouldBeNull();
        result.Value.ShouldBe(0);
        result.Concurrency.ShouldBeNull();
    }

    private static IReadOnlyDictionary<string, SuccessSpec> BuildSuccessSpecs()
        => new Dictionary<string, SuccessSpec>
        {
            ["single-key-product"] = new(
                IsComposite: false,
                BuildId: static () => Guid.NewGuid()),
            ["composite-key-review"] = new(
                IsComposite: true,
                BuildId: static () => Guid.NewGuid())
        };
}
