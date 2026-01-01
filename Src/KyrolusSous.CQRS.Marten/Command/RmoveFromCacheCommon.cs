namespace KyrolusSous.CQRS.Marten.Command;

public class RmoveFromCacheCommon(ICacheService cacheService)
{
    public async Task RemoveKeysByPatternAsync(bool cacheable, string pattern, CancellationToken cancellationToken = default)
    {
        if (!cacheable) return;
        await cacheService.RemoveKeysByPatternAsync(pattern, cancellationToken);
    }
}
