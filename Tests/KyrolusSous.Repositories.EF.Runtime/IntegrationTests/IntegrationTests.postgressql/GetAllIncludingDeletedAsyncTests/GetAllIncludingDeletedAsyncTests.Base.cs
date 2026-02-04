namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private static readonly AsyncLocal<string?> TenantScope = new();
    private sealed class TestCacheKeyContext : ICacheKeyContext
    {
        public string? ScopeKey => TenantScope.Value;
        public string? TenantId => TenantScope.Value;
    }
}
