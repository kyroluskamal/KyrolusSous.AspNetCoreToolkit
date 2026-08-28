namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllCompiledAsyncTests;

public partial class GetAllCompiledAsyncTests
{
    [Fact(DisplayName = "GetAllCompiledAsync works for non-soft-delete single-key repository")]
    public async Task GetAllCompiledAsync_NonSoftDeleteSingleKey_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeyRepositoryAsync<ApplicationDbContext, Payment, Guid>>();

        var items = await repo.GetAllCompiledAsync(p => p.Status == PaymentStatus.Paid);

        items.Count.ShouldBe(1);
        items[0].Status.ShouldBe(PaymentStatus.Paid);
    }

    [Fact(DisplayName = "GetAllCompiledAsync executes DB query when cache provider is missing even if cache policy is enabled")]
    public async Task GetAllCompiledAsync_NoCacheProvider_UsesDatabaseEveryTime()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllCompiledAsync
        };

        var customFactory = WithPolicy(policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IKyrolusCacheProvider>();
            });
        });

        using var scope = customFactory.Services.CreateScope();
        scope.ServiceProvider.GetService<IKyrolusCacheProvider>().ShouldBeNull();

        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var first = await repo.GetAllCompiledAsync(p => p.Price > 0m);
        var firstCommands = counter.Count;

        counter.Reset();
        var second = await repo.GetAllCompiledAsync(p => p.Price > 0m);
        var secondCommands = counter.Count;

        first.Count.ShouldBe(3);
        second.Count.ShouldBe(3);
        firstCommands.ShouldBeGreaterThan(0);
        secondCommands.ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "GetAllCompiledAsync returns empty list when cache provider GetOrCreate returns null")]
    public async Task GetAllCompiledAsync_CacheGetOrCreateReturnsNull_FallsBackToEmptyList()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllCompiledAsync
        };

        var customFactory = WithPolicy(policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IKyrolusCacheProvider>();
                services.AddSingleton<NullReturningGetOrCreateCacheProvider>();
                services.AddSingleton<IKyrolusCacheProvider>(sp => sp.GetRequiredService<NullReturningGetOrCreateCacheProvider>());
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<NullReturningGetOrCreateCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var items = await repo.GetAllCompiledAsync(p => p.Price > 0m);

        items.ShouldNotBeNull();
        items.ShouldBeEmpty();
        cache.GetOrCreateCalls.ShouldBe(1);
        cache.FactoryCalls.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);
    }

    private sealed class NullReturningGetOrCreateCacheProvider : IKyrolusCacheProvider
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
            if (factory is not null)
            {
                FactoryCalls++;
                _ = await factory(cancellationToken).ConfigureAwait(false);
            }

            return default!;
        }
    }
}
