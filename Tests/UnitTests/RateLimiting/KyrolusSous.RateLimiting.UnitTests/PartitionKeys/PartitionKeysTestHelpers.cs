namespace KyrolusSous.RateLimiting.UnitTests;

internal sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal sealed class RouteConfig
{
    public string? RouteId { get; init; }
    public string? ClusterId { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
