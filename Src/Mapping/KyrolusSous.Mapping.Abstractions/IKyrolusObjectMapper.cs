namespace KyrolusSous.Mapping.Abstractions;

/// <summary>
/// Defines the central object-to-object mapping and projection contract across the application.
/// </summary>
/// <remarks>
/// <para>
/// <b>Overview:</b>
/// <see cref="IKyrolusObjectMapper"/> provides high-speed, type-safe mapping between domain entities, DTOs, commands,
/// view models, and database records with <b>100% Native AOT compatibility</b> and zero runtime reflection overhead.
/// </para>
/// <para>
/// <b>Real-World Use Cases:</b>
/// <list type="bullet">
///   <item><description><b>API Presentation Layer:</b> Mapping sensitive database entities (<c>User</c>) to public response contracts (<c>UserResponseDto</c>) without exposing hashed passwords or internal metadata.</description></item>
///   <item><description><b>CQRS Commands:</b> Mapping incoming HTTP JSON request bodies (<c>CreateOrderRequest</c>) into validated CQRS commands (<c>CreateOrderCommand</c>).</description></item>
///   <item><description><b>Database Query Projections:</b> Projecting EF Core or Marten LINQ queries (<c>IQueryable.ProjectTo&lt;ProductDto&gt;()</c>) to emit optimized SQL <c>SELECT</c> clauses targeting only needed columns.</description></item>
///   <item><description><b>In-Place Entity Updates:</b> Applying incoming PATCH/PUT DTO fields onto an existing tracked EF Core entity (<c>Map(updateDto, existingEntity)</c>).</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IKyrolusObjectMapper
{
    /// <summary>
    /// Maps a source object of type <typeparamref name="TSource"/> to a new instance of <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TSource">The source object type.</typeparam>
    /// <typeparam name="TTarget">The target destination object type.</typeparam>
    /// <param name="source">The source instance to copy data from.</param>
    /// <returns>A newly instantiated and mapped <typeparamref name="TTarget"/> instance.</returns>
    /// <example>
    /// <code>
    /// var dto = mapper.Map&lt;User, UserDto&gt;(userEntity);
    /// </code>
    /// </example>
    TTarget Map<TSource, TTarget>(TSource source);

    /// <summary>
    /// Maps a source object to a new instance of <typeparamref name="TTarget"/> using an explicit <see cref="KyrolusMappingContext"/>
    /// for circular reference tracking, parameters, and custom resolution state.
    /// </summary>
    /// <typeparam name="TSource">The source object type.</typeparam>
    /// <typeparam name="TTarget">The target destination object type.</typeparam>
    /// <param name="source">The source instance to copy data from.</param>
    /// <param name="context">The contextual mapping state containing circular reference cache and custom items.</param>
    /// <returns>A newly mapped <typeparamref name="TTarget"/> instance.</returns>
    TTarget Map<TSource, TTarget>(TSource source, KyrolusMappingContext context);

    /// <summary>
    /// Maps a weakly-typed source object to a new instance of <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TTarget">The target destination object type.</typeparam>
    /// <param name="source">The weakly-typed source object.</param>
    /// <returns>A newly instantiated <typeparamref name="TTarget"/> instance.</returns>
    TTarget Map<TTarget>(object source);

    /// <summary>
    /// Maps a weakly-typed source object to a new instance of <typeparamref name="TTarget"/> with a <see cref="KyrolusMappingContext"/>.
    /// </summary>
    /// <typeparam name="TTarget">The target destination object type.</typeparam>
    /// <param name="source">The weakly-typed source object.</param>
    /// <param name="context">The contextual mapping state.</param>
    /// <returns>A newly instantiated <typeparamref name="TTarget"/> instance.</returns>
    TTarget Map<TTarget>(object source, KyrolusMappingContext context);

    /// <summary>
    /// Performs an in-place mapping, copying matching properties from <paramref name="source"/> onto an existing <paramref name="target"/> instance.
    /// </summary>
    /// <typeparam name="TSource">The source object type.</typeparam>
    /// <typeparam name="TTarget">The target destination object type.</typeparam>
    /// <param name="source">The source instance containing updated values.</param>
    /// <param name="target">The existing target instance to mutate.</param>
    /// <returns>The mutated <paramref name="target"/> instance.</returns>
    /// <example>
    /// <code>
    /// // Update existing tracked entity without replacing its reference in DbContext:
    /// mapper.Map(updateUserDto, existingUserEntity);
    /// </code>
    /// </example>
    TTarget Map<TSource, TTarget>(TSource source, TTarget target);

    /// <summary>
    /// Performs an in-place mapping with a contextual mapping state.
    /// </summary>
    /// <typeparam name="TSource">The source object type.</typeparam>
    /// <typeparam name="TTarget">The target destination object type.</typeparam>
    /// <param name="source">The source instance containing updated values.</param>
    /// <param name="target">The existing target instance to mutate.</param>
    /// <param name="context">The contextual mapping state.</param>
    /// <returns>The mutated <paramref name="target"/> instance.</returns>
    TTarget Map<TSource, TTarget>(TSource source, TTarget target, KyrolusMappingContext context);

    /// <summary>
    /// Maps an enumerable sequence of source items into an enumerable sequence of destination items.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TTarget">The destination element type.</typeparam>
    /// <param name="source">The collection of source elements.</param>
    /// <returns>A sequence of converted <typeparamref name="TTarget"/> items.</returns>
    IEnumerable<TTarget> MapEnumerable<TSource, TTarget>(IEnumerable<TSource> source);

    /// <summary>
    /// Maps a collection of source items into a pre-allocated <see cref="IReadOnlyList{TTarget}"/>.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TTarget">The destination element type.</typeparam>
    /// <param name="source">The collection of source elements.</param>
    /// <returns>A pre-allocated read-only list of converted destination items.</returns>
    IReadOnlyList<TTarget> MapList<TSource, TTarget>(IReadOnlyCollection<TSource> source);

    /// <summary>
    /// Projects an <see cref="IQueryable"/> query directly into a destination model using an expression tree,
    /// enabling the database provider (e.g. EF Core) to generate optimized SQL <c>SELECT</c> queries.
    /// </summary>
    /// <typeparam name="TTarget">The target projection type.</typeparam>
    /// <param name="source">The source LINQ queryable.</param>
    /// <returns>An <see cref="IQueryable{TTarget}"/> projection query.</returns>
    IQueryable<TTarget> ProjectTo<TTarget>(IQueryable source);

    /// <summary>
    /// Creates a deep clone of the specified source object, copying all nested reference graphs safely.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="source">The source instance to clone.</param>
    /// <returns>A new deep copy instance of <paramref name="source"/>.</returns>
    T Clone<T>(T source);

    /// <summary>
    /// Creates a deep clone of the specified source object using an explicit mapping context.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="source">The source instance to clone.</param>
    /// <param name="context">The mapping execution context.</param>
    /// <returns>A new deep copy instance of <paramref name="source"/>.</returns>
    T Clone<T>(T source, KyrolusMappingContext context);

    /// <summary>
    /// Retrieves the compiled or cached projection expression <see cref="Expression{TDelegate}"/> from <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <returns>The LINQ expression tree for projecting <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>.</returns>
    Expression<Func<TSource, TTarget>> GetProjection<TSource, TTarget>();
}
