namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync applies dynamic repository policy from policy provider")]
    public async Task GetAllAsync_DynamicPolicyProvider_AppliesPolicy()
    {
        var dynamicPolicy = new KyrolusRepositoryPolicy
        {
            UseSplitQueryDefault = true,
            AsNoTrackingDefault = true
        }.SetDefaultIncludeProperties<Product>("Reviews", "OrderLines", "ProductCategories");

        var provider = new RecordingRepositoryPolicyProvider(dynamicPolicy);
        var bootstrapPolicy = new KyrolusRepositoryPolicy { PolicyProvider = provider };
        var customFactory = WithPolicy(bootstrapPolicy);

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var first = (await repo.GetAllAsync()).ToList();
        first.Count.ShouldBe(3);
        counter.Count.ShouldBeGreaterThan(1); // split query + default collection includes

        var firstInvocationCount = provider.InvocationCount;
        firstInvocationCount.ShouldBe(1);
        provider.LastContext.ShouldNotBeNull();
        provider.LastContext!.EntityName.ShouldBe(nameof(Product));

        counter.Reset();
        _ = (await repo.GetAllAsync()).ToList();
        provider.InvocationCount.ShouldBe(firstInvocationCount); // policy initialized once per repository instance
    }

    [Fact(DisplayName = "GetAllAsync with Replace mode keeps explicit includes only")]
    public async Task GetAllAsync_DefaultIncludeMode_Replace_UsesExplicitIncludesOnly()
    {
        var dynamicPolicy = new KyrolusRepositoryPolicy
        {
            DefaultIncludeMode = KyrolusDefaultIncludeMode.Replace
        }.SetDefaultIncludeProperties<Product>("Store");

        var provider = new RecordingRepositoryPolicyProvider(dynamicPolicy);
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { PolicyProvider = provider });

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = (await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: false)).ToList();

        items.Count.ShouldBe(3);
        items.All(x => x.Reviews is not null).ShouldBeTrue();
        items.All(x => x.Store is null).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetAllAsync with Merge mode normalizes explicit include list")]
    public async Task GetAllAsync_DefaultIncludeMode_Merge_NormalizesExplicitIncludes()
    {
        var dynamicPolicy = new KyrolusRepositoryPolicy
        {
            DefaultIncludeMode = KyrolusDefaultIncludeMode.Merge
        }.SetDefaultIncludeProperties<Product>("Store");

        var provider = new RecordingRepositoryPolicyProvider(dynamicPolicy);
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { PolicyProvider = provider });

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = (await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: [" ", "Reviews", "Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: false)).ToList();

        items.Count.ShouldBe(3);
        items.All(x => x.Store is not null).ShouldBeTrue();
        items.All(x => x.Reviews is not null).ShouldBeTrue();
    }

    private sealed class RecordingRepositoryPolicyProvider(KyrolusRepositoryPolicy policy) : IKyrolusRepositoryPolicyProvider
    {
        private int count;
        public int InvocationCount => count;
        public KyrolusRepositoryPolicyContext? LastContext { get; private set; }

        public ValueTask<KyrolusRepositoryPolicy?> GetPolicyAsync(
            KyrolusRepositoryPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            LastContext = context;
            Interlocked.Increment(ref count);
            return ValueTask.FromResult<KyrolusRepositoryPolicy?>(policy);
        }
    }
}
