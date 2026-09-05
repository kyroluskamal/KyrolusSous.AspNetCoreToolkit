namespace KyrolusSous.CQRS.Mapping.Extensions;

/// <summary>
/// Provides extension methods connecting CQRS requests, commands, and results with <see cref="IKyrolusObjectMapper"/>.
/// </summary>
public static class KyrolusCqrsMappingExtensions
{
    /// <summary>
    /// Maps a command self-declaring its target entity via <see cref="IKyrolusMapTo{TTarget}"/> to a new <typeparamref name="TTarget"/> instance.
    /// </summary>
    /// <typeparam name="TTarget">The target domain entity type.</typeparam>
    /// <param name="command">The command instance.</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">An optional mapping context for custom parameters.</param>
    /// <returns>The newly created and mapped <typeparamref name="TTarget"/> instance.</returns>
    /// <remarks>
    /// Same mass-assignment exposure as <see cref="ApplyTo{TTarget}(IKyrolusMapTo{TTarget}, TTarget, IKyrolusObjectMapper, KyrolusMappingContext?)"/>:
    /// with no <c>CreateMap</c> registered for the pair, every public readable property on
    /// <paramref name="command"/> is copied onto the new <typeparamref name="TTarget"/> by
    /// case-insensitive name match. A command implementing <see cref="IKyrolusAllowListedMappedCommand"/>
    /// with a non-<see langword="null"/> <see cref="IKyrolusAllowListedMappedCommand.AllowedProperties"/>
    /// has every one of its own property names checked against that allow-list before mapping runs;
    /// a command that never sets it (the default) is mapped exactly as before.
    /// </remarks>
    public static TTarget ToEntity<TTarget>(
        this IKyrolusMapTo<TTarget> command,
        IKyrolusObjectMapper mapper,
        KyrolusMappingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(mapper);

        EnforceAllowList(command);

        return context is not null
            ? mapper.Map<TTarget>(command, context)
            : mapper.Map<TTarget>(command);
    }

    /// <summary>
    /// Maps any command or model to the specified destination <typeparamref name="TTarget"/> type.
    /// </summary>
    /// <typeparam name="TTarget">The target domain entity type.</typeparam>
    /// <param name="command">The command or source object.</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">An optional mapping context.</param>
    /// <returns>The newly created and mapped <typeparamref name="TTarget"/> instance.</returns>
    /// <remarks>
    /// See the <see cref="ToEntity{TTarget}(IKyrolusMapTo{TTarget}, IKyrolusObjectMapper, KyrolusMappingContext?)"/>
    /// remarks: a <paramref name="command"/> implementing <see cref="IKyrolusAllowListedMappedCommand"/> with a
    /// non-<see langword="null"/> allow-list is enforced here too; anything else is mapped unrestricted, as before.
    /// </remarks>
    public static TTarget ToEntity<TTarget>(
        this object command,
        IKyrolusObjectMapper mapper,
        KyrolusMappingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(mapper);

        EnforceAllowList(command);

        return context is not null
            ? mapper.Map<TTarget>(command, context)
            : mapper.Map<TTarget>(command);
    }

    /// <summary>
    /// Applies changes from a command self-declaring its target entity via <see cref="IKyrolusMapTo{TTarget}"/> onto an existing <paramref name="target"/> instance.
    /// </summary>
    /// <typeparam name="TTarget">The target domain entity type.</typeparam>
    /// <param name="command">The command instance carrying updated values.</param>
    /// <param name="target">The existing entity to update in place.</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">An optional mapping context.</param>
    /// <returns>The updated <paramref name="target"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// <b>Mass assignment:</b> with no <c>CreateMap</c> registered for the pair, mapping falls back to pure
    /// reflection over every public same-named property (case-insensitive) - so, with zero configuration,
    /// any public property <paramref name="command"/> happens to share with <typeparamref name="TTarget"/>
    /// gets copied onto it, including ones an API was never meant to expose for editing. A command mapping
    /// onto a persisted entity SHOULD implement <see cref="IKyrolusAllowListedMappedCommand"/> and set
    /// <see cref="IKyrolusAllowListedMappedCommand.AllowedProperties"/>: every one of the command's own
    /// property names is then checked against that allow-list (case-insensitively, mirroring
    /// <c>KyrolusPropertyAllowListBehavior</c>) before mapping runs, and a disallowed name throws
    /// <see cref="KyrolusSecurityException"/>. Omitting <see cref="IKyrolusAllowListedMappedCommand.AllowedProperties"/>
    /// (leaving it <see langword="null"/>, the default) preserves today's fully unrestricted behavior exactly.
    /// </para>
    /// <para>
    /// <b>Null handling:</b> <c>ApplyTo</c> models a partial update (PATCH), so unlike every other mapping
    /// call path a source property left <see langword="null"/> does NOT overwrite existing non-null data on
    /// <paramref name="target"/> by default. This method sets
    /// <see cref="KyrolusMappingContext.IgnoreNullValuesOnInPlaceMapKey"/> on the effective context unless the
    /// caller's own <paramref name="context"/> already carries a value for that key, so a caller who wants a
    /// specific call to overwrite with <see langword="null"/>s instead can pass a <paramref name="context"/>
    /// with that key set to <see langword="false"/>, or map via <c>mapper.Map(command, target)</c> directly.
    /// </para>
    /// </remarks>
    public static TTarget ApplyTo<TTarget>(
        this IKyrolusMapTo<TTarget> command,
        TTarget target,
        IKyrolusObjectMapper mapper,
        KyrolusMappingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(mapper);

        EnforceAllowList(command);

        return mapper.Map(command, target, WithNullSafeDefault(context));
    }

    /// <summary>
    /// Applies changes from any command or model onto an existing <paramref name="target"/> instance.
    /// </summary>
    /// <typeparam name="TTarget">The target domain entity type.</typeparam>
    /// <param name="command">The command or source object carrying updated values.</param>
    /// <param name="target">The existing entity to update in place.</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">An optional mapping context.</param>
    /// <returns>The updated <paramref name="target"/> instance.</returns>
    /// <remarks>
    /// See the <see cref="ApplyTo{TTarget}(IKyrolusMapTo{TTarget}, TTarget, IKyrolusObjectMapper, KyrolusMappingContext?)"/>
    /// remarks for the allow-list and null-handling defaults this method applies identically.
    /// </remarks>
    public static TTarget ApplyTo<TTarget>(
        this object command,
        TTarget target,
        IKyrolusObjectMapper mapper,
        KyrolusMappingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(mapper);

        EnforceAllowList(command);

        return mapper.Map(command, target, WithNullSafeDefault(context));
    }

    /// <summary>
    /// Rejects a source command implementing <see cref="IKyrolusAllowListedMappedCommand"/> with a
    /// non-<see langword="null"/> <see cref="IKyrolusAllowListedMappedCommand.AllowedProperties"/> that owns a
    /// public property name outside that allow-list.
    /// </summary>
    /// <remarks>
    /// Checked against the command's own public readable property names - the same set the mapping engine
    /// would otherwise attempt to read from - rather than the target's writable properties, so a rejection
    /// happens before <see cref="IKyrolusObjectMapper"/> touches the mapping target at all. A command
    /// that does not implement <see cref="IKyrolusAllowListedMappedCommand"/>, or whose
    /// <see cref="IKyrolusAllowListedMappedCommand.AllowedProperties"/> is <see langword="null"/>, is a no-op.
    /// The <see cref="IKyrolusAllowListedMappedCommand.AllowedProperties"/> member itself is excluded from the
    /// scan: a command overriding a default interface member becomes a public property on the concrete type
    /// (that is how C# surfaces an explicit override), so without this exclusion the allow-list's own
    /// metadata property would need to list itself to avoid rejecting itself.
    /// </remarks>
    private static void EnforceAllowList(object command)
    {
        if (command is not IKyrolusAllowListedMappedCommand { AllowedProperties: { } allowed })
        {
            return;
        }

        foreach (var property in command.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead ||
                string.Equals(property.Name, nameof(IKyrolusAllowListedMappedCommand.AllowedProperties), StringComparison.Ordinal))
            {
                continue;
            }

            var isAllowed = false;
            foreach (var candidate in allowed)
            {
                if (string.Equals(candidate, property.Name, StringComparison.OrdinalIgnoreCase))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (!isAllowed)
            {
                throw new KyrolusSecurityException(
                    $"Property '{property.Name}' is not in the allow-list for {command.GetType().Name}.");
            }
        }
    }

    /// <summary>
    /// Returns <paramref name="context"/> (creating one if <see langword="null"/>) with
    /// <see cref="KyrolusMappingContext.IgnoreNullValuesOnInPlaceMapKey"/> set unless the context already
    /// carries an explicit value for it.
    /// </summary>
    private static KyrolusMappingContext WithNullSafeDefault(KyrolusMappingContext? context)
    {
        var effectiveContext = context ?? new KyrolusMappingContext();
        if (!effectiveContext.Items.ContainsKey(KyrolusMappingContext.IgnoreNullValuesOnInPlaceMapKey))
        {
            effectiveContext.SetItem(KyrolusMappingContext.IgnoreNullValuesOnInPlaceMapKey, true);
        }

        return effectiveContext;
    }

    /// <summary>
    /// Maps a domain entity or source object into a destination DTO <typeparamref name="TDto"/>.
    /// </summary>
    /// <typeparam name="TDto">The destination DTO type.</typeparam>
    /// <param name="source">The source domain entity.</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">An optional mapping context.</param>
    /// <returns>The mapped <typeparamref name="TDto"/> instance.</returns>
    public static TDto ToDto<TDto>(
        this object source,
        IKyrolusObjectMapper mapper,
        KyrolusMappingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        return context is not null
            ? mapper.Map<TDto>(source, context)
            : mapper.Map<TDto>(source);
    }

    /// <summary>
    /// Maps an enumerable collection of source entities into a read-only list of destination DTOs.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TDto">The destination DTO element type.</typeparam>
    /// <param name="source">The collection of source entities.</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">An optional mapping context. Threaded through per-item mapping (rather than
    /// discarded) so a caller-supplied context - e.g. a <c>CreateMappingContext</c> override hook - is
    /// actually observed, mirroring the single-item <see cref="ToDto{TDto}(object, IKyrolusObjectMapper, KyrolusMappingContext?)"/>.</param>
    /// <returns>A read-only list of mapped <typeparamref name="TDto"/> instances.</returns>
    public static IReadOnlyList<TDto> ToDtoList<TSource, TDto>(
        this IEnumerable<TSource> source,
        IKyrolusObjectMapper mapper,
        KyrolusMappingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        var list = source as IReadOnlyCollection<TSource> ?? [.. source];
        return context is not null
            ? mapper.MapList<TSource, TDto>(list, context)
            : mapper.MapList<TSource, TDto>(list);
    }

    /// <summary>
    /// Maps a <see cref="KyrolusPagedResult{TSource}"/> into <see cref="KyrolusPagedResult{TTarget}"/>,
    /// converting each item to the destination type while preserving all pagination metadata.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TTarget">The target item type.</typeparam>
    /// <param name="pagedResult">The source paged result.</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">An optional mapping context, threaded through per-item mapping so a
    /// <c>CreateMappingContext</c> override hook on a paged request handler is actually observed.</param>
    /// <returns>A new <see cref="KyrolusPagedResult{TTarget}"/> with converted items.</returns>
    public static KyrolusPagedResult<TTarget> MapTo<TSource, TTarget>(
        this KyrolusPagedResult<TSource> pagedResult,
        IKyrolusObjectMapper mapper,
        KyrolusMappingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(pagedResult);
        ArgumentNullException.ThrowIfNull(mapper);

        var mappedItems = context is not null
            ? mapper.MapList<TSource, TTarget>(pagedResult.Items, context)
            : mapper.MapList<TSource, TTarget>(pagedResult.Items);
        return new KyrolusPagedResult<TTarget>(
            mappedItems,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize);
    }

    /// <summary>
    /// Maps a <see cref="KyrolusSeekResult{TSource}"/> into <see cref="KyrolusSeekResult{TTarget}"/>,
    /// converting each item to the destination type while preserving keyset/cursor pagination metadata.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TTarget">The target item type.</typeparam>
    /// <param name="seekResult">The source seek result.</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <param name="context">An optional mapping context, threaded through per-item mapping so a
    /// <c>CreateMappingContext</c> override hook on a seek request handler is actually observed.</param>
    /// <returns>A new <see cref="KyrolusSeekResult{TTarget}"/> with converted items.</returns>
    public static KyrolusSeekResult<TTarget> MapTo<TSource, TTarget>(
        this KyrolusSeekResult<TSource> seekResult,
        IKyrolusObjectMapper mapper,
        KyrolusMappingContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(seekResult);
        ArgumentNullException.ThrowIfNull(mapper);

        var mappedItems = context is not null
            ? mapper.MapList<TSource, TTarget>(seekResult.Items, context)
            : mapper.MapList<TSource, TTarget>(seekResult.Items);
        return new KyrolusSeekResult<TTarget>(
            mappedItems,
            seekResult.NextToken,
            seekResult.TotalCount,
            seekResult.PageSize);
    }
}
