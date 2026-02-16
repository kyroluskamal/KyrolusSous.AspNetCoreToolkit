namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetPagedAsyncTests;

public partial class GetPagedAsyncTests
{
    [Theory(DisplayName = "GetPagedAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, Review>
            {
                Filter = x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 5
            };

            var (items, totalCount) = await repo.GetPagedAsync(spec);
            items.Count.ShouldBe(1);
            totalCount.ShouldBe(1);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var spec = new TestPagedSpecification<Product, Product>
            {
                Filter = x => x.Id == DataSeeder.productLaptopId,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 5
            };

            var (items, totalCount) = await repo.GetPagedAsync(spec);
            items.Count.ShouldBe(1);
            totalCount.ShouldBe(1);
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetPagedAsync.Spec").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "GetPagedAsync.Spec")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }

    [Theory(DisplayName = "GetPagedAsync emits observer after with exception on cancellation")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedAsync_Observer_OnFailure(string caseId, bool compositeKey)
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
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 2
            };

            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.GetPagedAsync(spec, cts.Token));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var spec = new TestPagedSpecification<Product, Product>
            {
                Filter = x => x.Price > 0m,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 2
            };

            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.GetPagedAsync(spec, cts.Token));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetPagedAsync.Spec").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "GetPagedAsync.Spec")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }
}
