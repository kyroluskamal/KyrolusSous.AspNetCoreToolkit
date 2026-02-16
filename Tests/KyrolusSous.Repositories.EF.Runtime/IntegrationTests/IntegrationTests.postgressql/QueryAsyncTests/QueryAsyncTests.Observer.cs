namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.QueryAsyncTests;

public partial class QueryAsyncTests
{
    [Theory(DisplayName = "QueryAsync overload emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Overload_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var items = await repo.QueryAsync(
                x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
                x => x);
            items.Count.ShouldBe(1);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var items = await repo.QueryAsync(
                x => x.Id == DataSeeder.productLaptopId,
                x => x);
            items.Count.ShouldBe(1);
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "QueryAsync").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "QueryAsync")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }

    [Theory(DisplayName = "QueryAsync overload emits observer after with exception on cancellation")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Overload_Observer_OnFailure(string caseId, bool compositeKey)
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
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.QueryAsync(x => x.Rating > 0, x => x, cancellationToken: cts.Token));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.QueryAsync(x => x.Price > 0m, x => x, cancellationToken: cts.Token));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "QueryAsync").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "QueryAsync")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }

    [Theory(DisplayName = "QueryAsync specification emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Specification_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestQuerySpecification<Review, Review>
            {
                Filter = x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false
            };
            var items = await repo.QueryAsync(spec);
            items.Count.ShouldBe(1);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var spec = new TestQuerySpecification<Product, Product>
            {
                Filter = x => x.Id == DataSeeder.productLaptopId,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false
            };
            var items = await repo.QueryAsync(spec);
            items.Count.ShouldBe(1);
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "QueryAsync.Spec").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "QueryAsync.Spec")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }

    [Theory(DisplayName = "QueryAsync specification emits observer after with exception on cancellation")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Specification_Observer_OnFailure(string caseId, bool compositeKey)
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
            var spec = new TestQuerySpecification<Review, Review>
            {
                Filter = x => x.Rating > 0,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false
            };

            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.QueryAsync(spec, cts.Token));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var spec = new TestQuerySpecification<Product, Product>
            {
                Filter = x => x.Price > 0m,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false
            };

            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.QueryAsync(spec, cts.Token));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "QueryAsync.Spec").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "QueryAsync.Spec")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }
}
