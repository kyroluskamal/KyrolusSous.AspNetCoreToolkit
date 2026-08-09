namespace KyrolusSous.Mapping.Abstractions;

public interface IObjectMapper
{
    TTarget Map<TSource, TTarget>(TSource source);
    TTarget Map<TTarget>(object source);
    IEnumerable<TTarget> MapEnumerable<TSource, TTarget>(IEnumerable<TSource> source);
}
