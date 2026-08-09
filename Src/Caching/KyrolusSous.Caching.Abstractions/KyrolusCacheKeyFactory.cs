namespace KyrolusSous.Caching.Abstractions;

public sealed class KyrolusCacheKeyFactory(string? prefix = null) : IKyrolusCacheKeyFactory
{
    private readonly string? prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix!.Trim();

    public string BuildKey(string key, string? region = null, string? tenantId = null)
    {
        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(prefix)) parts.Add(prefix!);
        if (!string.IsNullOrWhiteSpace(region)) parts.Add(region!);
        if (!string.IsNullOrWhiteSpace(tenantId)) parts.Add(tenantId!);
        parts.Add(key);
        return string.Join(':', parts);
    }

    public string BuildTagKey(string tag, string? region = null, string? tenantId = null) =>
        BuildKey($"tag:{tag}", region, tenantId);

    public string BuildEntryTagsKey(string key) =>
        BuildKey($"tags:{key}");
}
