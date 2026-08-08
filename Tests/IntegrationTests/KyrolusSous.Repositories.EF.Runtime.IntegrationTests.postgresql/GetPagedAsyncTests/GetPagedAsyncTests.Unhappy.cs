namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetPagedAsyncTests;

public partial class GetPagedAsyncTests
{
    [Theory(DisplayName = "GetPagedAsync rejects null specifications")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedAsync_NullSpec_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () =>
                await repo.GetPagedAsync((IKyrolusPagedQuerySpecification<Review, ReviewPageProjection>)null!));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await singleRepo.GetPagedAsync((IKyrolusPagedQuerySpecification<Product, ProductPageProjection>)null!));
    }

    [Theory(DisplayName = "GetPagedAsync rejects null selectors")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedAsync_NullSelector_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, ReviewPageProjection>
            {
                Selector = null!,
                PageNumber = 1,
                PageSize = 2
            };
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.GetPagedAsync(spec));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleSpec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Selector = null!,
            PageNumber = 1,
            PageSize = 2
        };
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.GetPagedAsync(singleSpec));
    }

    public static TheoryData<string, int, int> InvalidPagingCases => new()
    {
        { "page-number-zero", 0, 2 },
        { "page-number-negative", -1, 2 },
        { "page-size-zero", 1, 0 },
        { "page-size-negative", 1, -5 }
    };

    [Theory(DisplayName = "GetPagedAsync rejects invalid paging values")]
    [MemberData(nameof(InvalidPagingCases))]
    public async Task GetPagedAsync_InvalidPaging_Throws(string caseId, int pageNumber, int pageSize)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var spec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Price > 0m,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await repo.GetPagedAsync(spec));
    }

    [Theory(DisplayName = "GetPagedAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, Review>
            {
                Filter = x => x.Rating > 0,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 2
            };
            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.GetPagedAsync(spec, cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleSpec = new TestPagedSpecification<Product, Product>
        {
            Filter = x => x.Price > 0m,
            Selector = x => x,
            AsNoTracking = true,
            UseSplitQuery = false,
            PageNumber = 1,
            PageSize = 2
        };
        await Should.ThrowAsync<OperationCanceledException>(async () => await singleRepo.GetPagedAsync(singleSpec, cts.Token));
    }
}
