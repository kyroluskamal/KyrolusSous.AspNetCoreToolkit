namespace KyrolusSous.CQRS.Caching;

public interface IKyrolusCacheKeyProvider
{
    string? GetCacheKey(object request);
    string? GetCachePattern(object request);
}
