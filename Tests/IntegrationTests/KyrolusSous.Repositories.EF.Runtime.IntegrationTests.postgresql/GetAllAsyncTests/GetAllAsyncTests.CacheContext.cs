using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync cache scope falls back to tenant when scope key is empty")]
    public async Task GetAllAsync_CacheKey_UsesTenantFallback_WhenScopeKeyMissing()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IKyrolusCacheKeyContext>();
                services.AddScoped<IKyrolusCacheKeyContext>(sp =>
                {
                    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
                    var tenant = http?.Request?.Headers["X-Tenant-Id"].ToString();
                    return new TenantOnlyCacheKeyContext(tenant);
                });
            });
        });

        using var client = customFactory.CreateClient();
        using var scope = customFactory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        static HttpRequestMessage Build(string tenant)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/product");
            request.Headers.Add("X-Tenant-Id", tenant);
            return request;
        }

        (await client.SendAsync(Build("tenant-a"))).EnsureSuccessStatusCode();
        cache.Count.ShouldBe(1);

        (await client.SendAsync(Build("tenant-a"))).EnsureSuccessStatusCode();
        cache.Count.ShouldBe(1);

        (await client.SendAsync(Build("tenant-b"))).EnsureSuccessStatusCode();
        cache.Count.ShouldBe(2);
    }

    private sealed class TenantOnlyCacheKeyContext(string? tenantId) : IKyrolusCacheKeyContext
    {
        public string? ScopeKey => null;
        public string? TenantId => tenantId;
    }
}
