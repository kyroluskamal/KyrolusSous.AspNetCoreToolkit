namespace KyrolusSous.Caching.Abstractions;

public interface IKyrolusCacheKeyFactory
{
    string BuildKey(string key, string? region = null, string? tenantId = null);
    string BuildTagKey(string tag, string? region = null, string? tenantId = null);
    string BuildEntryTagsKey(string key);
}
