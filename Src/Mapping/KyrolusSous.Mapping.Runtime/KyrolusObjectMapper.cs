namespace KyrolusSous.Mapping.Runtime;

/// <summary>
/// Default thread-safe implementation of <see cref="IKyrolusObjectMapper"/> powered by high-performance compiled expression trees.
/// </summary>
public sealed class KyrolusObjectMapper : IKyrolusObjectMapper
{
    private readonly KyrolusMappingConfiguration _configuration;
    private readonly KyrolusExpressionMappingEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusObjectMapper"/> class with the specified configuration.
    /// </summary>
    /// <param name="configuration">The mapping configuration container.</param>
    public KyrolusObjectMapper(KyrolusMappingConfiguration? configuration = null)
    {
        _configuration = configuration ?? new KyrolusMappingConfiguration();
        _engine = new KyrolusExpressionMappingEngine(_configuration);
    }

    /// <inheritdoc />
    public TTarget Map<TSource, TTarget>(TSource source)
    {
        var context = new KyrolusMappingContext();
        return Map<TSource, TTarget>(source, context);
    }

    /// <inheritdoc />
    public TTarget Map<TSource, TTarget>(TSource source, KyrolusMappingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (source is null)
        {
            return default!;
        }

        var result = _engine.Map(typeof(TSource), typeof(TTarget), source, context, this);
        return (TTarget)result!;
    }

    /// <inheritdoc />
    public TTarget Map<TTarget>(object source)
    {
        var context = new KyrolusMappingContext();
        return Map<TTarget>(source, context);
    }

    /// <inheritdoc />
    public TTarget Map<TTarget>(object source, KyrolusMappingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (source is null)
        {
            return default!;
        }

        var result = _engine.Map(source.GetType(), typeof(TTarget), source, context, this);
        return (TTarget)result!;
    }

    /// <inheritdoc />
    public TTarget Map<TSource, TTarget>(TSource source, TTarget target)
    {
        var context = new KyrolusMappingContext();
        return Map(source, target, context);
    }

    /// <inheritdoc />
    public TTarget Map<TSource, TTarget>(TSource source, TTarget target, KyrolusMappingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (source is null || target is null)
        {
            return target;
        }

        _engine.MapInPlace(typeof(TSource), typeof(TTarget), source, target, context, this);
        return target;
    }

    /// <inheritdoc />
    public IEnumerable<TTarget> MapEnumerable<TSource, TTarget>(IEnumerable<TSource> source)
    {
        if (source is null)
        {
            return [];
        }

        var context = new KyrolusMappingContext();
        var list = new List<TTarget>();

        foreach (var item in source)
        {
            list.Add(Map<TSource, TTarget>(item, context));
        }

        return list;
    }

    /// <inheritdoc />
    public IReadOnlyList<TTarget> MapList<TSource, TTarget>(IReadOnlyCollection<TSource> source)
    {
        if (source is null || source.Count == 0)
        {
            return [];
        }

        var context = new KyrolusMappingContext();
        var list = new List<TTarget>(source.Count);

        foreach (var item in source)
        {
            list.Add(Map<TSource, TTarget>(item, context));
        }

        return list;
    }

    /// <inheritdoc />
    public T Clone<T>(T source)
    {
        var context = new KyrolusMappingContext();
        return Clone(source, context);
    }

    /// <inheritdoc />
    public T Clone<T>(T source, KyrolusMappingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (source is null)
        {
            return default!;
        }

        var result = _engine.Map(typeof(T), typeof(T), source, context, this);
        return (T)result!;
    }

    /// <inheritdoc />
    public IQueryable<TTarget> ProjectTo<TTarget>(IQueryable source)
    {
        return source.ProjectTo<TTarget>(this);
    }

    /// <inheritdoc />
    public Expression<Func<TSource, TTarget>> GetProjection<TSource, TTarget>()
    {
        return KyrolusQueryableProjectionExtensions.GetOrCreateProjectionExpression<TSource, TTarget>();
    }
}
