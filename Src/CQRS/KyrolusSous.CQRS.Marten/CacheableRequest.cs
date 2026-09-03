using KyrolusSous.CQRS.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.Marten;

public class CacheableRequest(bool isCacheable) : IKyrolusCacheableRequest
{
    public bool Cacheable { get; set; } = isCacheable;
}
