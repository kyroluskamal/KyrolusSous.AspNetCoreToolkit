namespace KyrolusSous.Repositories.EF.Generator.TestApp;

public sealed class SimpleCacheKeyContext(string? scopeKey) : IKyrolusCacheKeyContext
{
    public string? ScopeKey { get; } = scopeKey;
}
