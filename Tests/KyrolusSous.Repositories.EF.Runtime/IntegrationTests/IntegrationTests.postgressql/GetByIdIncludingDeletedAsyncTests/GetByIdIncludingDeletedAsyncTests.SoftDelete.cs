namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    public static TheoryData<string, bool> SoftDeleteCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync returns soft-deleted entities")]
    [MemberData(nameof(SoftDeleteCases))]
    public async Task GetByIdIncludingDeletedAsync_SoftDeletedEntity_Works(string caseId, bool isComposite)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (isComposite)
        {
            await WithReviewSoftDeleted(async (repo, _) =>
            {
                var hidden = await repo.GetByIdAsync(ExistingReviewKey);
                hidden.ShouldBeNull();

                var included = await repo.GetByIdIncludingDeletedAsync(ExistingReviewKey);
                included.ShouldNotBeNull();
                included.IsDeleted.ShouldBeTrue();
                included.ProductId.ShouldBe(DataSeeder.productLaptopId);
                included.CustomerId.ShouldBe(DataSeeder.customerJaneId);
            });
            return;
        }

        await WithProductSoftDeleted(async (repo, _) =>
        {
            var hidden = await repo.GetByIdAsync(ExistingDeletedProductId);
            hidden.ShouldBeNull();

            var included = await repo.GetByIdIncludingDeletedAsync(ExistingDeletedProductId);
            included.ShouldNotBeNull();
            included.IsDeleted.ShouldBeTrue();
            included.Id.ShouldBe(ExistingDeletedProductId);
        });
    }
}
