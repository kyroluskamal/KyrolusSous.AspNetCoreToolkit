namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = true")]
    public async Task GetAllAsync_AsNoTracking_ReturnsEntitiesWithAsNoTracking()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();
        // Act
        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
        // Assert
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = false")]
    public async Task GetAllAsync_AsNoTrackingFalse_ReturnsEntitiesWithAsNoTrackingFalse()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: false,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = null and Policy.AsNoTrackingDefault == true")]
    public async Task GetAllAsync_AsNoTracking_Null_AsNoTrackingDefaultFromPolicy_true()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = null and Policy.AsNoTrackingDefault == false")]
    public async Task GetAllAsync_AsNoTracking_Null_AsNoTrackingDefaultFromPolicy_false()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    [Fact(DisplayName = "GetAllAsync uses default AsNoTracking when asNoTracking = null and Policy.AsNoTrackingDefault == null")]
    public async Task GetAllAsync_AsNoTracking_Null_AsNoTrackingDefault_Null_UsesDefaultPolicy()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = null });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
}
