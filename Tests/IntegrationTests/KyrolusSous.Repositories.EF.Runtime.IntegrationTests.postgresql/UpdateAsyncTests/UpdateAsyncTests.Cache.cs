using System.Collections.Concurrent;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateAsyncTests;

public partial class UpdateAsyncTests
{
    [Fact(DisplayName = "UpdateAsync invalidates GetById, GetByIdCompiled, and GetByIdIncludingDeleted caches")]
    public async Task UpdateAsync_Invalidates_ById_CacheVariants()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations =
                KyrolusCacheReadOperations.GetByIdAsync
                | KyrolusCacheReadOperations.GetByIdCompiledAsync
                | KyrolusCacheReadOperations.GetByIdIncludingDeletedAsync
                | KyrolusCacheReadOperations.GetAllAsync
                | KyrolusCacheReadOperations.GetAllCompiledAsync
                | KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
                | KyrolusCacheReadOperations.GetDeletedOnlyAsync
        };

        var customFactory = WithPolicy(policy);
        var entity = CreateValidProduct(name: "cache-invalidation-target", sku: $"UPD-CACHE-{Guid.NewGuid():N}");
        await using (var prepScope = customFactory.Services.CreateAsyncScope())
        {
            var db = prepScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Add(entity);
            await db.SaveChangesAsync();
        }

        try
        {
            using (var scope = customFactory.Services.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
                var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

                cache.Clear();

                _ = await repo.GetByIdAsync(entity.Id);
                _ = await repo.GetByIdCompiledAsync(entity.Id);
                _ = await repo.GetByIdIncludingDeletedAsync(entity.Id);

                counter.Reset();
                _ = await repo.GetByIdAsync(entity.Id);
                _ = await repo.GetByIdCompiledAsync(entity.Id);
                _ = await repo.GetByIdIncludingDeletedAsync(entity.Id);
                counter.Count.ShouldBe(0);
            }

            await using (var updateScope = customFactory.Services.CreateAsyncScope())
            {
                var repo = updateScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var uow = updateScope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

                var updated = Clone(entity);
                updated.Name = "cache-invalidation-updated";
                _ = await repo.UpdateAsync(updated);
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            }

            using (var verifyScope = customFactory.Services.CreateScope())
            {
                var repo = verifyScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var counter = verifyScope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

                counter.Reset();
                _ = await repo.GetByIdAsync(entity.Id);
                _ = await repo.GetByIdCompiledAsync(entity.Id);
                _ = await repo.GetByIdIncludingDeletedAsync(entity.Id);
                counter.Count.ShouldBeGreaterThan(0);

                counter.Reset();
                _ = await repo.GetByIdAsync(entity.Id);
                _ = await repo.GetByIdCompiledAsync(entity.Id);
                _ = await repo.GetByIdIncludingDeletedAsync(entity.Id);
                counter.Count.ShouldBe(0);
            }
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Fact(DisplayName = "UpdateAsync applies extra invalidation templates and skips id templates when by-id reads are disabled")]
    public async Task UpdateAsync_ExtraInvalidationTemplates_SkipIdTemplates_WhenByIdReadsDisabled()
    {
        var dynamicPolicyProvider = new RecordingDynamicCachePolicyProvider(
            new KyrolusCachePolicy(
                Enabled: true,
                ExtraInvalidationKeys: ["dyn:plain"],
                ExtraInvalidationKeyPatterns: ["dyn:pattern:*"]));

        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(
                Enabled: true,
                ExtraInvalidationKeys:
                [
                    "base:plain",
                    "base:id:{id}",
                    "base:key:{key}",
                    "base:all:{all}"
                ],
                ExtraInvalidationKeyPatterns: ["base:pattern:*"]),
            CachePolicyProvider = dynamicPolicyProvider,
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IKyrolusRepositoryCachePolicyProvider>();
                services.AddSingleton<IKyrolusRepositoryCachePolicyProvider>(dynamicPolicyProvider);
            });
        });
        var entity = CreateValidProduct(name: "extra-invalidation-target", sku: $"UPD-EXTRA-{Guid.NewGuid():N}");
        await using (var prepScope = customFactory.Services.CreateAsyncScope())
        {
            var db = prepScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Add(entity);
            await db.SaveChangesAsync();
        }

        try
        {
            using var scope = customFactory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

            cache.Clear();
            await cache.SetAsync("base:plain", 1);
            await cache.SetAsync("dyn:plain", 1);
            await cache.SetAsync("base:pattern:1", 1);
            await cache.SetAsync("dyn:pattern:1", 1);
            await cache.SetAsync("base:id:{id}", 1);
            await cache.SetAsync("base:key:{key}", 1);

            var updated = Clone(entity);
            updated.Name = "extra-invalidation-updated";
            _ = await repo.UpdateAsync(updated);
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            (await cache.ExistsAsync("base:plain")).ShouldBeFalse();
            (await cache.ExistsAsync("base:pattern:1")).ShouldBeFalse();

            // {id}/{key} templates are skipped because GetById cache reads are disabled in policy.
            (await cache.ExistsAsync("base:id:{id}")).ShouldBeTrue();
            (await cache.ExistsAsync("base:key:{key}")).ShouldBeTrue();

            dynamicPolicyProvider.Operations.ShouldContain(nameof(KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>.GetAllAsync));
            dynamicPolicyProvider.Operations.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    private sealed class RecordingDynamicCachePolicyProvider(KyrolusCachePolicy policy) : IKyrolusRepositoryCachePolicyProvider
    {
        private readonly ConcurrentBag<string> operations = new();
        public IReadOnlyCollection<string> Operations => operations.ToArray();

        public ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
            KyrolusRepositoryCachePolicyContext context,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(context.Operation))
                operations.Add(context.Operation!);
            return ValueTask.FromResult<KyrolusCachePolicy?>(policy);
        }
    }
}
