namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllCompiledAsyncTests;

public partial class GetAllCompiledAsyncTests
{
    [Fact(DisplayName = "GetAllCompiledAsync applies global filters for single-key entities")]
    public async Task GetAllCompiledAsync_GlobalFilter_Works_ForSingleKey()
    {
        var policy = new KyrolusRepositoryPolicy()
            .AddGlobalWhereFilter<Product>(p => p.Price > 100m);

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllCompiledAsync(p => p.Price > 0m);

        items.Count.ShouldBe(2);
        items.All(x => x.Price > 100m).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetAllCompiledAsync applies global filters for composite-key entities")]
    public async Task GetAllCompiledAsync_GlobalFilter_Works_ForCompositeKey()
    {
        var policy = new KyrolusRepositoryPolicy()
            .AddGlobalWhereFilter<Review>(r => r.Rating >= 4);

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var items = await repo.GetAllCompiledAsync(r => r.Rating > 0);

        items.Count.ShouldBe(2);
        items.All(x => x.Rating >= 4).ShouldBeTrue();
    }
}
