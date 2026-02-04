namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = true - single key")]
    public async Task GetByIdAsync_AsNoTracking_SingleKey()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(Guid.Parse(productLaptopId), asNoTracking: true);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = true - composite key")]
    public async Task GetByIdAsync_AsNoTracking_CompositeKey()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(CompositeKey_ProductReview, asNoTracking: true);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = false")]
    public async Task GetByIdAsync_AsNoTracking_False()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(Guid.Parse(productLaptopId), asNoTracking: false);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "GetByIdAsync uses policy AsNoTrackingDefault = true when asNoTracking is null")]
    public async Task GetByIdAsync_AsNoTracking_Null_PolicyTrue()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(Guid.Parse(productLaptopId), asNoTracking: null);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "GetByIdAsync uses policy AsNoTrackingDefault = false when asNoTracking is null")]
    public async Task GetByIdAsync_AsNoTracking_Null_PolicyFalse()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(Guid.Parse(productLaptopId), asNoTracking: null);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
}
