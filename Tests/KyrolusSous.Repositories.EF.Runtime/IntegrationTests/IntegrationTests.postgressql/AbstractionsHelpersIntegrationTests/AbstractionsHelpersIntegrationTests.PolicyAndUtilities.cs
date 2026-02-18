namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AbstractionsHelpersIntegrationTests;

public sealed class AbstractionsHelpersIntegrationTests_PolicyAndUtilities(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    [Fact(DisplayName = "Repository policy extensions compose multiple global filters and apply them to real queryable")]
    public async Task PolicyExtensions_GlobalFilters_ComposeAndApply()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var policy = new KyrolusRepositoryPolicy()
            .AddGlobalWhereFilter<Product>(p => p.Price > 100m)
            .AddGlobalWhereFilter<Product>(p => p.StockQuantity < 30);

        var filter = policy.GetGlobalQueryFilter<Product>();
        filter.ShouldNotBeNull();

        var filtered = await filter!(db.Products.AsNoTracking()).ToListAsync();
        filtered.Count.ShouldBe(1);
        filtered.Single().Name.ShouldBe("Laptop Pro 15");
    }
}
