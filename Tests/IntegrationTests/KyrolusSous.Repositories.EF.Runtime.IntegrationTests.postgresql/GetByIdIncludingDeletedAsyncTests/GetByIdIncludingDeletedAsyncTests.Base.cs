namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private static readonly AsyncLocal<string?> TenantScope = new();

    private sealed class TestCacheKeyContext : ICacheKeyContext
    {
        public string? ScopeKey => TenantScope.Value;
        public string? TenantId => TenantScope.Value;
    }
}
