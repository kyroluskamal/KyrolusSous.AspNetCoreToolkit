namespace KyrolusSous.Caching.Abstractions;

public sealed record KyrolusCacheInvalidationMessage(
    KyrolusCacheInvalidationKind Kind,
    IReadOnlyCollection<string> Values);
