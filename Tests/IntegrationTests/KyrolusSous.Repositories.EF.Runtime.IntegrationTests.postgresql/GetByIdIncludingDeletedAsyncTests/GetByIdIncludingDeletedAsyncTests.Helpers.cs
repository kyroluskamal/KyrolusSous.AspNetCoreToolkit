namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    protected static Guid ExistingProductId => DataSeeder.productLaptopId;
    protected static Guid ExistingDeletedProductId => DataSeeder.productHeadphonesId;
    protected static Guid MissingProductId => Guid.Parse("66666666-6666-6666-6666-666666666699");

    protected static object[] ExistingReviewKey => DataSeeder.ReviewLapTopKey;
    protected static object[] MissingReviewKey => [DataSeeder.productLaptopId, Guid.Parse("77777777-7777-7777-7777-777777777799")];
    protected static object[] ExistingReviewKeyReversed => [ExistingReviewKey[1], ExistingReviewKey[0]];

    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task WithProductSoftDeleted(
        Func<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>, IServiceProvider, Task> body,
        KyrolusRepositoryPolicy? policy = null)
    {
        var customFactory = policy is null ? Factory : WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = sp.GetRequiredService<IKyrolusUnitOfWork>();

        var deleted = false;
        try
        {
            deleted = await repo.SoftDeleteAsync(ExistingDeletedProductId);
            deleted.ShouldBeTrue();
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            await body(repo, sp);
        }
        finally
        {
            if (deleted)
            {
                await repo.RestoreAsync(ExistingDeletedProductId);
                await uow.SaveChangesAsync();
            }
        }
    }

    protected async Task WithReviewSoftDeleted(
        Func<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>, IServiceProvider, Task> body,
        KyrolusRepositoryPolicy? policy = null)
    {
        var customFactory = policy is null ? Factory : WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
        var uow = sp.GetRequiredService<IKyrolusUnitOfWork>();

        var deleted = false;
        try
        {
            deleted = await repo.SoftDeleteAsync(ExistingReviewKey);
            deleted.ShouldBeTrue();
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            await body(repo, sp);
        }
        finally
        {
            if (deleted)
            {
                await repo.RestoreAsync(ExistingReviewKey);
                await uow.SaveChangesAsync();
            }
        }
    }
}
