namespace KyrolusSous.Repositories.EF.Generator.TestApp;

public sealed class SimpleCacheKeyContext(string? scopeKey) : ICacheKeyContext
{
    public string? ScopeKey { get; } = scopeKey;
}
