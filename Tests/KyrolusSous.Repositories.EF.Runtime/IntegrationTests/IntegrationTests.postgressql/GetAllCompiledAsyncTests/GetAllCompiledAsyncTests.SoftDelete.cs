namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllCompiledAsyncTests;

public partial class GetAllCompiledAsyncTests
{
    [Fact(DisplayName = "GetAllCompiledAsync excludes soft-deleted single-key entities")]
    public async Task GetAllCompiledAsync_SoftDelete_Works_ForSingleKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        await repo.SoftDeleteAsync(DataSeeder.productHeadphonesId);
        await uow.SaveChangesAsync();

        try
        {
            var items = await repo.GetAllCompiledAsync(p => p.Price > 0m);
            items.Count.ShouldBe(2);
            items.Any(x => x.Id == DataSeeder.productHeadphonesId).ShouldBeFalse();
        }
        finally
        {
            await repo.RestoreAsync(DataSeeder.productHeadphonesId);
            await uow.SaveChangesAsync();
        }
    }

    [Fact(DisplayName = "GetAllCompiledAsync excludes soft-deleted composite-key entities")]
    public async Task GetAllCompiledAsync_SoftDelete_Works_ForCompositeKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        var keyValues = DataSeeder.ReviewLapTopKey;

        await repo.SoftDeleteAsync(keyValues);
        await uow.SaveChangesAsync();

        try
        {
            var items = await repo.GetAllCompiledAsync(r => r.Rating > 0);
            items.Count.ShouldBe(2);
            items.Any(x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId).ShouldBeFalse();
        }
        finally
        {
            await repo.RestoreAsync(keyValues);
            await uow.SaveChangesAsync();
        }
    }
}
