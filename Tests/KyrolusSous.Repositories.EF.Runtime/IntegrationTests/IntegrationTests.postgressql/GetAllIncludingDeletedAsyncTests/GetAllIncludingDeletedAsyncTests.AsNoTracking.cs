namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync respects AsNoTracking")]
    public async Task GetAllIncludingDeletedAsync_AsNoTracking_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync respects AsNoTracking = false")]
    public async Task GetAllIncludingDeletedAsync_AsNoTrackingFalse_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: null,
            asNoTracking: false,
            useSplitQuery: null,
            cancellationToken: default);

        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses AsNoTrackingDefault from policy when asNoTracking is null")]
    public async Task GetAllIncludingDeletedAsync_AsNoTracking_Null_UsesPolicyDefaultTrue()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: null,
            asNoTracking: null,
            useSplitQuery: null,
            cancellationToken: default);

        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses AsNoTrackingDefault=false from policy when asNoTracking is null")]
    public async Task GetAllIncludingDeletedAsync_AsNoTracking_Null_UsesPolicyDefaultFalse()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: null,
            asNoTracking: null,
            useSplitQuery: null,
            cancellationToken: default);

        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync defaults AsNoTracking to true when policy default is null")]
    public async Task GetAllIncludingDeletedAsync_AsNoTracking_Null_UsesDefaultWhenPolicyNull()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = null });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        dbContext.ChangeTracker.Clear();

        await repo.GetAllIncludingDeletedAsync(
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
