namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests
{
    [Fact(DisplayName = "GetByIdCompiledAsync works for non-soft-delete single-key repository")]
    public async Task GetByIdCompiledAsync_NonSoftDeleteSingleKey_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeyRepositoryAsync<ApplicationDbContext, Payment, Guid>>();

        var item = await repo.GetByIdCompiledAsync(DataSeeder.orderId);

        item.ShouldNotBeNull();
        item!.OrderId.ShouldBe(DataSeeder.orderId);
        item.Status.ShouldBe(PaymentStatus.Paid);
    }

    [Fact(DisplayName = "GetByIdCompiledAsync executes DB query when cache provider is missing even if cache policy is enabled")]
    public async Task GetByIdCompiledAsync_NoCacheProvider_UsesDatabaseEveryTime()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdCompiledAsync
        };

        var customFactory = WithPolicy(policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICacheProvider>();
            });
        });

        using var scope = customFactory.Services.CreateScope();
        scope.ServiceProvider.GetService<ICacheProvider>().ShouldBeNull();

        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var first = await repo.GetByIdCompiledAsync(ExistingProductId);
        var firstCommands = counter.Count;

        counter.Reset();
        var second = await repo.GetByIdCompiledAsync(ExistingProductId);
        var secondCommands = counter.Count;

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        firstCommands.ShouldBeGreaterThan(0);
        secondCommands.ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "GetByIdCompiledAsync returns null when cache provider GetOrCreate returns null")]
    public async Task GetByIdCompiledAsync_CacheGetOrCreateReturnsNull_ReturnsNull()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdCompiledAsync
        };

        var customFactory = WithPolicy(policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICacheProvider>();
                services.AddSingleton<NullReturningGetOrCreateCacheProvider>();
                services.AddSingleton<ICacheProvider>(sp => sp.GetRequiredService<NullReturningGetOrCreateCacheProvider>());
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<NullReturningGetOrCreateCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var item = await repo.GetByIdCompiledAsync(ExistingProductId);

        item.ShouldBeNull();
        cache.GetOrCreateCalls.ShouldBe(1);
        cache.FactoryCalls.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "GetByIdCompiledAsync falls back when entity has composite key and throws for single key argument")]
    public async Task GetByIdCompiledAsync_CompositeEntityInSingleKeyRepo_ThrowsArgumentException()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = new KyrolusSingleKeyRepositoryAsync<ApplicationDbContext, Review, Guid>(db);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await repo.GetByIdCompiledAsync(DataSeeder.productLaptopId));
    }

    private sealed class NullReturningGetOrCreateCacheProvider : ICacheProvider
    {
        public int GetOrCreateCalls { get; private set; }
        public int FactoryCalls { get; private set; }

        public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult(default(T?));
        public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
            => Task.FromResult<IDictionary<string, T?>>(new Dictionary<string, T?>());
        public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<T> GetOrCreateAsync<T>(
            string cacheKey,
            Func<CancellationToken, Task<T>> factory,
            KyrolusCacheEntryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            GetOrCreateCalls++;
            _ = options;
            _ = cacheKey;

            if (factory is not null)
            {
                FactoryCalls++;
                _ = await factory(cancellationToken).ConfigureAwait(false);
            }

            return default!;
        }
    }
}
