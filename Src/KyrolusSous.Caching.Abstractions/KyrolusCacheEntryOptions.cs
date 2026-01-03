namespace KyrolusSous.Caching.Abstractions;

public sealed class KyrolusCacheEntryOptions
{
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public TimeSpan? Jitter { get; set; }
    public TimeSpan? NegativeExpirationRelativeToNow { get; set; }
    public IReadOnlyCollection<string>? Tags { get; set; }
    public string? Region { get; set; }
    public string? TenantId { get; set; }
}
