using KyrolusSous.CQRS.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF;

public class CacheableRequest(bool isCacheable) : ICacheableRequest
{
    public bool Cacheable { get; set; } = isCacheable;
}
