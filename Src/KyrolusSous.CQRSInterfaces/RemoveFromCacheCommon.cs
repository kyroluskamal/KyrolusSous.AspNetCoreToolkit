global using KyrolusSous.RedisCaching.Services;

namespace KyrolusSous.CQRSInterfaces;

public class RemoveFromCacheCommon(ICacheService cacheService)
{
    public async Task RemoveKeysByPatternAsync(bool cacheable, string pattern, CancellationToken cancellationToken = default)
    {
        if (!cacheable) return;
        await cacheService.RemoveKeysByPatternAsync(pattern, cancellationToken);
    }
}
