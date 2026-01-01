namespace KyrolusSous.CQRS.Abstractions.Interfaces;

public interface ICacheableRequest
{
    public bool Cacheable { get; set; }

}
