namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateRangeAsyncTests;

public partial class UpdateRangeAsyncTests
{
    public static TheoryData<string, bool> ObserverCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateRangeAsync emits observer before and after on success")]
    [MemberData(nameof(ObserverCases))]
    public async Task UpdateRangeAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var seed = new List<Review>
            {
                CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "update-range-observer-before-1"),
                CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "update-range-observer-before-2")
            };
            await SeedReviewsAsync(seed);

            try
            {
                var updates = seed.Select(Clone).ToList();
                updates[0].Comment = "update-range-observer-after-1";
                updates[1].Comment = "update-range-observer-after-2";

                var result = (await repo.UpdateRangeAsync(updates)).ToList();
                result.Count.ShouldBe(2);
            }
            finally
            {
                await CleanupReviewsAsync(seed.Select(x => (x.ProductId, x.CustomerId)));
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var seed = new List<Product>
            {
                CreateValidProduct(name: "update-range-observer-before-1"),
                CreateValidProduct(name: "update-range-observer-before-2")
            };
            await SeedProductsAsync(seed);

            try
            {
                var updates = seed.Select(Clone).ToList();
                updates[0].Name = "update-range-observer-after-1";
                updates[1].Name = "update-range-observer-after-2";

                var result = (await repo.UpdateRangeAsync(updates)).ToList();
                result.Count.ShouldBe(2);
            }
            finally
            {
                await CleanupProductsAsync(seed.Select(x => x.Id));
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "UpdateRangeAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "UpdateRangeAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "UpdateRangeAsync emits observer after with exception on mixed found and missing entities")]
    [MemberData(nameof(ObserverCases))]
    public async Task UpdateRangeAsync_Observer_OnFailure(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var seed = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "update-range-observer-mixed-existing");
            await SeedReviewsAsync([seed]);

            try
            {
                var updates = new List<Review>
                {
                    Clone(seed),
                    CreateValidReview(Guid.NewGuid(), DataSeeder.customerJohnId, rating: 5, comment: "missing")
                };
                updates[0].Comment = "existing-updated";

                await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.UpdateRangeAsync(updates));
            }
            finally
            {
                await CleanupReviewsAsync([(seed.ProductId, seed.CustomerId)]);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var seed = CreateValidProduct(name: "update-range-observer-mixed-existing");
            await SeedProductsAsync([seed]);

            try
            {
                var updates = new List<Product>
                {
                    Clone(seed),
                    CreateValidProduct(id: Guid.NewGuid(), name: "missing")
                };
                updates[0].Name = "existing-updated";

                await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.UpdateRangeAsync(updates));
            }
            finally
            {
                await CleanupProductsAsync([seed.Id]);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "UpdateRangeAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "UpdateRangeAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<KeyNotFoundException>();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
