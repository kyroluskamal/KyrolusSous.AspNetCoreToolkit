namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    [Fact(DisplayName = "GetByIdAsync does not return soft-deleted entities")]
    public async Task GetByIdAsync_DoesNotReturnSoftDeletedEntities()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        var product = await repo.GetByIdAsync(Guid.Parse(productHeadphonesId));

        product.ShouldNotBeNull();
        try
        {
            var x = await repo.SoftDeleteAsync(Guid.Parse(productHeadphonesId));
            var result = await uow.SaveChangesAsync();
            x.ShouldBeTrue();
            result.ShouldBe(1);

            var item = await repo.GetByIdAsync(Guid.Parse(productHeadphonesId));
            item.ShouldBeNull();
        }
        finally
        {
            await repo.RestoreAsync(Guid.Parse(productHeadphonesId));
            await uow.SaveChangesAsync();
        }
    }
}
