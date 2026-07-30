using KyrolusSous.Mapping.Abstractions;
using Mapster;

namespace KyrolusSous.Mapping.Mapster;

public sealed class MapsterObjectMapper : IObjectMapper
{
    // Mapster returns null only when source is null; IObjectMapper declares TTarget non-nullable,
    // so callers are contractually required to pass a non-null source.
    public TTarget Map<TSource, TTarget>(TSource source) => source.Adapt<TTarget>()!;

    public TTarget Map<TTarget>(object source) => source.Adapt<TTarget>();

    public IEnumerable<TTarget> MapEnumerable<TSource, TTarget>(IEnumerable<TSource> source) =>
        source.Adapt<IEnumerable<TTarget>>();
}
