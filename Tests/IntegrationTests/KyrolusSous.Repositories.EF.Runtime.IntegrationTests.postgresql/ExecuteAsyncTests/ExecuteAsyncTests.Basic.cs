namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteAsyncTests;

public partial class ExecuteAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record ExecuteSuccessSpec(bool IsComposite, bool UseTransaction);

    private static readonly IReadOnlyDictionary<string, ExecuteSuccessSpec> SuccessSpecs = BuildSuccessSpecs();
    public static TheoryData<string> SuccessCases => CaseIdsFrom(SuccessSpecs);

    [Theory(DisplayName = "ExecuteAsync persists changes and returns success")]
    [MemberData(nameof(SuccessCases))]
    public async Task ExecuteAsync_Success_PersistsChanges(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SuccessSpecs[caseId];

        if (spec.IsComposite)
        {
            var product = CreateValidProduct(name: $"execute-comp-product-{Guid.NewGuid():N}");
            var review = CreateValidReview(
                productId: product.Id,
                customerId: DataSeeder.customerJohnId,
                rating: 4,
                comment: $"execute-comp-review-{Guid.NewGuid():N}");
            await SeedProductAsync(product);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

                var result = await uow.ExecuteAsync(
                    work: () =>
                    {
                        db.Reviews.Add(review);
                        return Task.CompletedTask;
                    },
                    useTransaction: spec.UseTransaction);

                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeGreaterThan(0);

                var persisted = await FindReviewAsync(review.ProductId, review.CustomerId);
                persisted.ShouldNotBeNull();
                persisted!.Comment.ShouldBe(review.Comment);
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
                await CleanupProductAsync(product.Id);
            }

            return;
        }

        var single = CreateValidProduct(name: $"execute-single-{Guid.NewGuid():N}");

        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            var result = await uow.ExecuteAsync(
                work: () =>
                {
                    db.Products.Add(single);
                    return Task.CompletedTask;
                },
                useTransaction: spec.UseTransaction,
                rowVersionPropertyName: "RowVersion");

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeGreaterThan(0);

            var persisted = await FindProductAsync(single.Id);
            persisted.ShouldNotBeNull();
            persisted!.Name.ShouldBe(single.Name);
        }
        finally
        {
            await CleanupProductAsync(single.Id);
        }
    }

    private static IReadOnlyDictionary<string, ExecuteSuccessSpec> BuildSuccessSpecs()
        => new Dictionary<string, ExecuteSuccessSpec>
        {
            ["single-with-transaction"] = new(IsComposite: false, UseTransaction: true),
            ["single-without-transaction"] = new(IsComposite: false, UseTransaction: false),
            ["composite-with-transaction"] = new(IsComposite: true, UseTransaction: true),
            ["composite-without-transaction"] = new(IsComposite: true, UseTransaction: false)
        };
}
