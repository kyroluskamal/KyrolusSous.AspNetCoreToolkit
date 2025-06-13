using KyrolusSous.CQRSInterfaces.Interfaces;

namespace KyrolusSous.CQRS.Base;

public class CacheableRequest(bool isCacheable) : ICacheableRequest
{
    public bool Cacheable { get; set; } = isCacheable;
}