namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    private sealed record SoftDeleteSpec(Func<GetByIdAsyncTests, Task> Run);

    private static readonly IReadOnlyDictionary<string, SoftDeleteSpec> SoftDeleteSpecs = BuildSoftDeleteSpecs();

    public static TheoryData<string> SoftDeleteCases => CaseIdsFrom(SoftDeleteSpecs);

    [Theory(DisplayName = "GetByIdAsync does not return soft-deleted entities")]
    [MemberData(nameof(SoftDeleteCases))]
    public Task GetByIdAsync_DoesNotReturnSoftDeletedEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return SoftDeleteSpecs[caseId].Run(this);
    }

    private static IReadOnlyDictionary<string, SoftDeleteSpec> BuildSoftDeleteSpecs()
        => new Dictionary<string, SoftDeleteSpec>
        {
            ["single"] = new SoftDeleteSpec(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
                var product = await repo.GetByIdAsync(Guid.Parse(productHeadphonesId));

                product.ShouldNotBeNull();
                try
                {
                    var deleted = await repo.SoftDeleteAsync(Guid.Parse(productHeadphonesId));
                    var result = await uow.SaveChangesAsync();
                    deleted.ShouldBeTrue();
                    result.ShouldBeGreaterThan(0);

                    var item = await repo.GetByIdAsync(Guid.Parse(productHeadphonesId));
                    item.ShouldBeNull();
                }
                finally
                {
                    await repo.RestoreAsync(Guid.Parse(productHeadphonesId));
                    await uow.SaveChangesAsync();
                }
            }),
            ["composite"] = new SoftDeleteSpec(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
                var review = await repo.GetByIdAsync(CompositeKey_ProductReview);

                review.ShouldNotBeNull();
                try
                {
                    var deleted = await repo.SoftDeleteAsync(CompositeKey_ProductReview);
                    var result = await uow.SaveChangesAsync();
                    deleted.ShouldBeTrue();
                    result.ShouldBeGreaterThan(0);

                    var item = await repo.GetByIdAsync(CompositeKey_ProductReview);
                    item.ShouldBeNull();
                }
                finally
                {
                    await repo.RestoreAsync(CompositeKey_ProductReview);
                    await uow.SaveChangesAsync();
                }
            })
        };
}
