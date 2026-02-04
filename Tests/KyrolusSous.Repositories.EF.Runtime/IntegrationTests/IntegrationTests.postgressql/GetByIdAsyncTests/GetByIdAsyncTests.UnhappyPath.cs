namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    [Fact(DisplayName = "GetByIdAsync throws when include string is invalid navigation")]
    public async Task GetByIdAsync_InvalidIncludeString_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await repo.GetByIdAsync(
                Guid.Parse(productLaptopId),
                includeProperties: ["NotARealNavigation"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: default);
        });
    }

    [Fact(DisplayName = "GetByIdAsync throws when composite key length is invalid")]
    public async Task GetByIdAsync_CompositeKey_InvalidLength_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await repo.GetByIdAsync([Guid.Parse(productLaptopId)]);
        });
    }

    [Fact(DisplayName = "GetByIdAsync returns 404 when composite key order is wrong")]
    public async Task GetByIdAsync_CompositeKey_OrderMatters()
    {
        var reversed = new[] { CompositeKey_ProductReview[1], CompositeKey_ProductReview[0] };
        var (response, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(reversed);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        review.ShouldBeNull();
    }
}
