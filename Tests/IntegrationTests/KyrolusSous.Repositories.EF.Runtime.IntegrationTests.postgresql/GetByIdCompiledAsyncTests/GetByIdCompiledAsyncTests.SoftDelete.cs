namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests
{
    [Fact(DisplayName = "GetByIdCompiledAsync does not return soft-deleted entities")]
    public async Task GetByIdCompiledAsync_DoesNotReturnSoftDeletedEntity()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var existing = await repo.GetByIdCompiledAsync(DataSeeder.productHeadphonesId);
        existing.ShouldNotBeNull();

        try
        {
            var deleted = await repo.SoftDeleteAsync(DataSeeder.productHeadphonesId);
            var result = await uow.SaveChangesAsync();
            deleted.ShouldBeTrue();
            result.ShouldBeGreaterThan(0);

            var item = await repo.GetByIdCompiledAsync(DataSeeder.productHeadphonesId);
            item.ShouldBeNull();
        }
        finally
        {
            await repo.RestoreAsync(DataSeeder.productHeadphonesId);
            await uow.SaveChangesAsync();
        }
    }
}
