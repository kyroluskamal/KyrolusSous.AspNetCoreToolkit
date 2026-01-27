namespace KyrolusSous.Repositories.EF.Runtime.TestApp;

public sealed class SimpleCacheKeyContext(string? scopeKey) : ICacheKeyContext
{
    public string? ScopeKey { get; } = scopeKey;
}

