using System.Reflection;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Repositories.EF.Abstractions;
using KyrolusSous.Repositories.EF.Abstractions.Policy;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class KyrolusRepositoryAsyncInternalsUnitTests
{
    [Fact(DisplayName = "RepositoryAsync cache key generation omits scope segment when cache key context has no scope and no tenant")]
    public void RepositoryAsync_CacheKeyAll_NoScopeOrTenant_OmitsScopeSegment()
    {
        using var db = CreateDbContext();
        var repo = new ProbeRepository(
            db,
            cacheKeyContext: new StaticCacheKeyContext(scopeKey: " ", region: null, tenantId: " "));

        var key = repo.ExposeCacheKeyAll(nameof(KyrolusSingleKeySoftDeleteRepositoryAsync<ProbeDbContext, ProbeEntity, Guid>.GetAllAsync), policySuffix: null);

        key.ShouldContain("ProbeEntity:op=GetAllAsync:all");
        key.ShouldNotContain(":scope=");
    }

    [Fact(DisplayName = "RepositoryAsync cache key fingerprint encodes null key values as literal null")]
    public void RepositoryAsync_CacheKeyById_NullKeyValue_EncodesAsNullLiteral()
    {
        using var db = CreateDbContext();
        var repo = new ProbeRepository(db);

        var key = repo.ExposeCacheKeyById(nameof(KyrolusSingleKeySoftDeleteRepositoryAsync<ProbeDbContext, ProbeEntity, Guid>.GetByIdAsync), [null], policySuffix: null);

        key.ShouldContain("0=null");
    }

    [Fact(DisplayName = "RepositoryAsync invalidation template expansion handles blank entries and expands id template to both id cache variants")]
    public void RepositoryAsync_ExpandInvalidationTemplates_BlankAndIdTemplates_Work()
    {
        var repositoryType = typeof(KyrolusRepositoryAsync<ProbeDbContext, ProbeEntity, Guid>);
        var expandMethod = repositoryType.GetMethod("ExpandInvalidationTemplates", BindingFlags.Static | BindingFlags.NonPublic);
        expandMethod.ShouldNotBeNull();

        var ctx = new KyrolusInvalidationContext(
            Entity: nameof(ProbeEntity),
            Tenant: null,
            Scope: null,
            PolicySuffix: null,
            KeyFingerprint: "0=key",
            AllKey: "all-key",
            AllCompiledKey: "all-compiled-key",
            IdKey: "id-key",
            IdCompiledKey: "id-compiled-key");

        var templates = new[] { " ", "extra:{id}" };
        var expanded = ((IEnumerable<string>)expandMethod!.Invoke(null, [templates, ctx])!).ToList();

        expanded.Count.ShouldBe(2);
        expanded.ShouldContain("extra:id-key");
        expanded.ShouldContain("extra:id-compiled-key");
    }

    [Fact(DisplayName = "RepositoryAsync private ResolvePolicyIncludes returns null when include mode is Replace and explicit includes are provided")]
    public void RepositoryAsync_ResolvePolicyIncludes_ReplaceModeWithExplicitIncludes_ReturnsNull()
    {
        using var db = CreateDbContext();
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultIncludeMode = KyrolusDefaultIncludeMode.Replace
        }.SetDefaultIncludeProperties<ProbeEntity>("Name");
        var repo = new ProbeRepository(db, policy: policy);

        var repositoryType = typeof(KyrolusRepositoryAsync<ProbeDbContext, ProbeEntity, Guid>);
        var method = repositoryType.GetMethod("ResolvePolicyIncludes", BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();

        var result = method!.Invoke(repo, [new List<string> { "Count" }, null, null]);
        result.ShouldBeNull();
    }

    [Fact(DisplayName = "RepositoryAsync private BuildCompiledGetAllFiltered wraps internal build errors in InvalidOperationException")]
    public void RepositoryAsync_BuildCompiledGetAllFiltered_NullFilter_ThrowsWrappedInvalidOperationException()
    {
        var repositoryType = typeof(KyrolusRepositoryAsync<ProbeDbContext, ProbeEntity, Guid>);
        var method = repositoryType.GetMethod("BuildCompiledGetAllFiltered", BindingFlags.Static | BindingFlags.NonPublic);
        method.ShouldNotBeNull();

        var ex = Should.Throw<TargetInvocationException>(() =>
            method!.Invoke(null, [false, "IsDeleted", Array.Empty<string>(), null!, false, false]));

        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
        ex.InnerException!.Message.ShouldContain("Failed to build compiled GetAll query.");
    }

    private static ProbeDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ProbeDbContext>()
            .UseInMemoryDatabase($"repo-internals-{Guid.NewGuid():N}")
            .Options);

    private sealed class ProbeRepository : KyrolusSingleKeySoftDeleteRepositoryAsync<ProbeDbContext, ProbeEntity, Guid>
    {
        public ProbeRepository(
            ProbeDbContext db,
            KyrolusRepositoryPolicy? policy = null,
            ICacheKeyContext? cacheKeyContext = null)
            : base(
                db,
                policy,
                observer: null,
                bulkExecutor: null,
                cache: null,
                enableCaching: false,
                cacheTtlSeconds: null,
                cacheKeyContext: cacheKeyContext,
                cachePolicyProvider: null,
                policyProvider: null)
        {
        }

        public string ExposeCacheKeyAll(string operation, string? policySuffix) => CacheKeyAll(operation, policySuffix);
        public string ExposeCacheKeyById(string operation, object?[]? keyValues, string? policySuffix) => CacheKeyById(operation, keyValues, policySuffix);
    }

    private sealed class StaticCacheKeyContext(string? scopeKey, string? region, string? tenantId) : ICacheKeyContext
    {
        public string? ScopeKey => scopeKey;
        public string? Region => region;
        public string? TenantId => tenantId;
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
    {
        public DbSet<ProbeEntity> Entities => Set<ProbeEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProbeEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name);
                entity.Property(e => e.Count);
                entity.Property(e => e.IsDeleted);
            });
        }
    }

    private sealed class ProbeEntity
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Count { get; init; }
        public bool IsDeleted { get; init; }
    }
}
