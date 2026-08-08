namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetPagedWithDefaultsAsyncTests;

public partial class GetPagedWithDefaultsAsyncTests
{
    public static TheoryData<string, bool, int, int?> InvalidPageSizeCases => new()
    {
        { "single-no-policy-default", false, 0, null },
        { "composite-no-policy-default", true, 0, null },
        { "single-zero-policy-default", false, -3, 0 },
        { "composite-zero-policy-default", true, -1, 0 }
    };

    [Theory(DisplayName = "GetPagedWithDefaultsAsync rejects null specifications")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedWithDefaultsAsync_NullSpec_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () =>
                await repo.GetPagedWithDefaultsAsync((IKyrolusPagedQuerySpecification<Review, ReviewPageProjection>)null!));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await singleRepo.GetPagedWithDefaultsAsync((IKyrolusPagedQuerySpecification<Product, ProductPageProjection>)null!));
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync rejects invalid effective page size")]
    [MemberData(nameof(InvalidPageSizeCases))]
    public async Task GetPagedWithDefaultsAsync_InvalidPageSize_Throws(
        string caseId,
        bool compositeKey,
        int specificationPageSize,
        int? policyPageSize)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var customFactory = policyPageSize.HasValue
            ? WithPolicy(new KyrolusRepositoryPolicy { DefaultPageSize = policyPageSize })
            : Factory;
        using var scope = customFactory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, ReviewPageProjection>
            {
                Filter = x => x.Rating > 0,
                Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                PageNumber = 1,
                PageSize = specificationPageSize
            };
            await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await repo.GetPagedWithDefaultsAsync(spec));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleSpec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Price > 0m,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            PageNumber = 1,
            PageSize = specificationPageSize
        };
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await singleRepo.GetPagedWithDefaultsAsync(singleSpec));
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedWithDefaultsAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, ReviewPageProjection>
            {
                Filter = x => x.Rating > 0,
                Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                PageNumber = 1,
                PageSize = 2
            };
            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.GetPagedWithDefaultsAsync(spec, cancellationToken: cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleSpec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Price > 0m,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            PageNumber = 1,
            PageSize = 2
        };
        await Should.ThrowAsync<OperationCanceledException>(async () => await singleRepo.GetPagedWithDefaultsAsync(singleSpec, cancellationToken: cts.Token));
    }
}
