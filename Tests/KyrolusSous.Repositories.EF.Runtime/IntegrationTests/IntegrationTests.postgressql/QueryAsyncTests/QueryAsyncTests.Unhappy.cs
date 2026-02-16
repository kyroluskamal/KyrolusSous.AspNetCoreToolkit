namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.QueryAsyncTests;

public partial class QueryAsyncTests
{
    [Theory(DisplayName = "QueryAsync overload rejects null selector")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Overload_NullSelector_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () =>
                await repo.QueryAsync<ReviewQueryProjection>(x => x.Rating > 0, null!));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await singleRepo.QueryAsync<ProductQueryProjection>(x => x.Price > 0m, null!));
    }

    [Theory(DisplayName = "QueryAsync overload respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Overload_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.QueryAsync(x => x.Rating > 0, x => x, cancellationToken: cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await singleRepo.QueryAsync(x => x.Price > 0m, x => x, cancellationToken: cts.Token));
    }

    [Fact(DisplayName = "QueryAsync specification rejects null specification")]
    public async Task QueryAsync_Specification_NullSpec_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await repo.QueryAsync((IKyrolusQuerySpecification<Product, ProductQueryProjection>)null!));
    }

    [Theory(DisplayName = "QueryAsync specification rejects null selector")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Specification_NullSelector_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestQuerySpecification<Review, ReviewQueryProjection>
            {
                Selector = null!,
                AsNoTracking = true,
                UseSplitQuery = false
            };
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.QueryAsync(spec));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleSpec = new TestQuerySpecification<Product, ProductQueryProjection>
        {
            Selector = null!,
            AsNoTracking = true,
            UseSplitQuery = false
        };
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.QueryAsync(singleSpec));
    }

    [Theory(DisplayName = "QueryAsync specification respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Specification_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestQuerySpecification<Review, Review>
            {
                Filter = x => x.Rating > 0,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false
            };
            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.QueryAsync(spec, cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleSpec = new TestQuerySpecification<Product, Product>
        {
            Filter = x => x.Price > 0m,
            Selector = x => x,
            AsNoTracking = true,
            UseSplitQuery = false
        };
        await Should.ThrowAsync<OperationCanceledException>(async () => await singleRepo.QueryAsync(singleSpec, cts.Token));
    }
}
