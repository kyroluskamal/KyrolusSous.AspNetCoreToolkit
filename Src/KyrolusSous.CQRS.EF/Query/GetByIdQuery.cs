namespace KyrolusSous.CQRS.EF.Query;

public class GetByIdQuery<TResponse, TKey>(TKey id, bool cacheable = false) : CacheableRequest(cacheable), IKyrolusQuery<TResponse?>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public List<string>? IncludeProperties { get; set; }
    public TKey Id { get; set; } = id;

}
