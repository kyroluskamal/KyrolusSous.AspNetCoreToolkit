namespace KyrolusSous.Repositories.EF.Runtime.TestApp;

public sealed class SimpleCacheKeyContext(string? scopeKey) : IKyrolusCacheKeyContext
{
    public string? ScopeKey { get; } = scopeKey;
}

