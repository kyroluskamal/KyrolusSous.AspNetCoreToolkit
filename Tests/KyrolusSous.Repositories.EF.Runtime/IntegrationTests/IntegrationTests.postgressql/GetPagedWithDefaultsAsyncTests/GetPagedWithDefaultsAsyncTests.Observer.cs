namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetPagedWithDefaultsAsyncTests;

public partial class GetPagedWithDefaultsAsyncTests
{
    [Theory(DisplayName = "GetPagedWithDefaultsAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedWithDefaultsAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, ReviewPageProjection>
            {
                Filter = x => x.Rating >= 3,
                Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                OrderBy = q => q.OrderByDescending(x => x.Rating),
                AsNoTracking = true,
                PageNumber = 1,
                PageSize = 2
            };

            var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(spec);
            items.ShouldNotBeEmpty();
            totalCount.ShouldBeGreaterThan(0);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var spec = new TestPagedSpecification<Product, ProductPageProjection>
            {
                Filter = x => x.Price >= 35m,
                Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
                OrderBy = q => q.OrderBy(x => x.Price),
                AsNoTracking = true,
                PageNumber = 1,
                PageSize = 2
            };

            var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(spec);
            items.ShouldNotBeEmpty();
            totalCount.ShouldBeGreaterThan(0);
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetPagedWithDefaultsAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "GetPagedWithDefaultsAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync emits observer after with exception on cancellation")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedWithDefaultsAsync_Observer_OnFailure(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, Review>
            {
                Filter = x => x.Rating > 0,
                Selector = x => x,
                AsNoTracking = true,
                PageNumber = 1,
                PageSize = 2
            };

            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.GetPagedWithDefaultsAsync(spec, cancellationToken: cts.Token));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var spec = new TestPagedSpecification<Product, Product>
            {
                Filter = x => x.Price > 0m,
                Selector = x => x,
                AsNoTracking = true,
                PageNumber = 1,
                PageSize = 2
            };

            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.GetPagedWithDefaultsAsync(spec, cancellationToken: cts.Token));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetPagedWithDefaultsAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "GetPagedWithDefaultsAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
