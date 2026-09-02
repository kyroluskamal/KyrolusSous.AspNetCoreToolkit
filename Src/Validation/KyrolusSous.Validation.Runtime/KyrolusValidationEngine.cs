namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// Default <see cref="IKyrolusValidationEngine"/> implementation. Coordinates every registered
/// <see cref="IKyrolusRequestValidator{TRequest}"/> for a request type and, around that, applies profiles, result
/// caching, RuleSet/Group/Severity filtering, error-code and field-path mapping, localization, and before/after
/// hooks - in that order - so every validator kind (Fluent, DataAnnotations, FluentValidation, hand-written)
/// gets identical cross-cutting behavior regardless of how its rules were authored.
/// </summary>
/// <remarks>
/// Registered via <see cref="ServiceCollectionExtensions.AddKyrolusValidationRuntime"/>. All constructor
/// dependencies besides <paramref name="serviceProvider"/> are optional: a caching, localization, or mapping
/// dependency left unregistered simply disables that pipeline step rather than causing a failure.
/// </remarks>
/// <param name="serviceProvider">Used to resolve the <see cref="IKyrolusRequestValidator{TRequest}"/> instances and hooks registered for the request type being validated.</param>
/// <param name="localizer">Optional localizer used to translate each failure's <see cref="KyrolusValidationFailure.ErrorMessage"/>. When <see langword="null"/>, messages are returned as-authored.</param>
/// <param name="cacheStore">Optional cache store. When <see langword="null"/>, result caching is skipped even for requests implementing <see cref="IKyrolusValidationCacheable"/>.</param>
/// <param name="cacheKeyProvider">Optional cache key provider. Required (alongside <paramref name="cacheStore"/>) for caching to take effect.</param>
/// <param name="errorCodeMapper">Optional hook to rewrite each failure's <see cref="KyrolusValidationFailure.ErrorCode"/> before it's returned.</param>
/// <param name="fieldPathMapper">Optional hook to rewrite each failure's <see cref="KyrolusValidationFailure.FieldPath"/> before it's returned.</param>
/// <param name="hookOrderLookup">Optional resolver for <see cref="KyrolusValidationHookOrderAttribute"/>-declared hook order, normally the generated implementation from <c>KyrolusSous.Validation.Generator</c>. When <see langword="null"/>, or when it has no entry for a given hook's type, ordering falls back to <see cref="IKyrolusValidationHook.Order"/>.</param>
public sealed class KyrolusValidationEngine(
    IServiceProvider serviceProvider,
    IKyrolusLocalizer? localizer = null,
    IKyrolusValidationCacheStore? cacheStore = null,
    IKyrolusValidationCacheKeyProvider? cacheKeyProvider = null,
    IKyrolusValidationErrorCodeMapper? errorCodeMapper = null,
    IKyrolusValidationFieldPathMapper? fieldPathMapper = null,
    IKyrolusValidationHookOrderLookup? hookOrderLookup = null) : IKyrolusValidationEngine
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IKyrolusLocalizer? localizer = localizer;
    private readonly IKyrolusValidationCacheStore? cacheStore = cacheStore;
    private readonly IKyrolusValidationCacheKeyProvider? cacheKeyProvider = cacheKeyProvider;
    private readonly IKyrolusValidationErrorCodeMapper? errorCodeMapper = errorCodeMapper;
    private readonly IKyrolusValidationFieldPathMapper? fieldPathMapper = fieldPathMapper;
    private readonly IKyrolusValidationHookOrderLookup? hookOrderLookup = hookOrderLookup;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
    => await ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync<TRequest>(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveContext = ApplyProfiles(context);

        await RunBeforeHooks(request, effectiveContext, cancellationToken).ConfigureAwait(false);

        try
        {
            var cacheEntry = ResolveCacheEntry(request, effectiveContext);
            if (cacheEntry is not null && cacheStore is not null)
            {
                var cached = await cacheStore.TryGetAsync(cacheEntry.Key, cancellationToken).ConfigureAwait(false);
                if (cached is not null)
                {
                    var cachedResult = localizer is null
                        ? cached
                        : [.. cached.Select(failure => failure with { ErrorMessage = LocalizeFailure(localizer, failure) })];

                    await RunAfterHooks(request, effectiveContext, cachedResult, cancellationToken).ConfigureAwait(false);
                    return cachedResult;
                }
            }

            var validators = serviceProvider.GetServices<IKyrolusRequestValidator<TRequest>>().ToArray();
            if (validators.Length == 0)
                return await ReturnEmptyResultAsync(request, effectiveContext, cacheEntry, cancellationToken).ConfigureAwait(false);

            List<KyrolusValidationFailure> failures = [];
            foreach (var validator in validators)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<KyrolusValidationFailure> result;
                if (validator is IKyrolusRequestValidatorWithContext<TRequest> contextValidator)
                {
                    result = await contextValidator.ValidateAsync(request, effectiveContext, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
                }
                if (result is not null && result.Count > 0)
                {
                    failures.AddRange(result);
                }
            }

            if (failures.Count == 0)
                return await ReturnEmptyResultAsync(request, effectiveContext, cacheEntry, cancellationToken).ConfigureAwait(false);

            var normalized = failures.Select(NormalizeFailure).ToArray();
            var mapped = ApplyMappings(normalized, effectiveContext);
            var filtered = ApplyFilters(mapped, effectiveContext);
            // Only a populated result is "negative" (a failure) in the caching sense - RuleSet/Group/Severity
            // filtering can still bring filtered.Count back down to zero even though raw failures existed, and
            // that outcome is a pass as far as the caller is concerned, so it gets the normal (longer) TTL too.
            TimeSpan? cacheTtlOverride = filtered.Count > 0 ? ResolveNegativeTtl(request) : null;
            await TryStoreCache(cacheEntry, filtered, cancellationToken, cacheTtlOverride).ConfigureAwait(false);

            if (localizer is null)
            {
                await RunAfterHooks(request, effectiveContext, filtered, cancellationToken).ConfigureAwait(false);
                return filtered;
            }

            var localized = filtered
                .Select(failure => failure with { ErrorMessage = LocalizeFailure(localizer, failure) })
                .ToArray();
            await RunAfterHooks(request, effectiveContext, localized, cancellationToken).ConfigureAwait(false);
            return localized;
        }
        catch (Exception)
        {
            // A hook's "before" half already ran (e.g. KyrolusValidationTracingHook opened an Activity,
            // KyrolusValidationMetricsHook started a Stopwatch). Without this, a validator or the cache store
            // throwing here meant "after" never ran: the Activity leaked un-disposed/never exported, and no
            // metric was recorded for the very validations most worth observing. IKyrolusValidationHook has no
            // exception-aware overload, so this reports an empty failures list rather than skipping cleanup.
            await RunAfterHooks(request, effectiveContext, [], cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateBatchAsync<TRequest>(
        IEnumerable<TRequest> requests,
        CancellationToken cancellationToken = default)
    => await ValidateBatchAsync(requests, KyrolusValidationContext.Default, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateBatchAsync<TRequest>(
        IEnumerable<TRequest> requests,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var allFailures = new List<KyrolusValidationFailure>();
        var index = 0;
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var itemFailures = await ValidateAsync(request, context, cancellationToken).ConfigureAwait(false);
            foreach (var failure in itemFailures)
            {
                var path = string.IsNullOrWhiteSpace(failure.FieldPath) ? failure.PropertyName : failure.FieldPath;
                allFailures.Add(failure with { FieldPath = $"[{index}].{path}" });
            }

            index++;
        }

        return allFailures;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond>(
        TFirst first,
        TSecond second,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second);
        return ValidateAsync(composite, KyrolusValidationContext.Default, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond>(
        TFirst first,
        TSecond second,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second);
        return ValidateAsync(composite, context, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird>(
        TFirst first,
        TSecond second,
        TThird third,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second, third);
        return ValidateAsync(composite, KyrolusValidationContext.Default, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird>(
        TFirst first,
        TSecond second,
        TThird third,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second, third);
        return ValidateAsync(composite, context, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird, TFourth>(
        TFirst first,
        TSecond second,
        TThird third,
        TFourth fourth,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second, third, fourth);
        return ValidateAsync(composite, KyrolusValidationContext.Default, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird, TFourth>(
        TFirst first,
        TSecond second,
        TThird third,
        TFourth fourth,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second, third, fourth);
        return ValidateAsync(composite, context, cancellationToken);
    }

    /// <summary>
    /// Resolves each name in <see cref="KyrolusValidationContext.Profiles"/> via the registered
    /// <see cref="IKyrolusValidationProfileProvider"/> and merges their RuleSets, Groups, and MinimumSeverity into
    /// <paramref name="context"/> (union for RuleSets/Groups, the strictest value for MinimumSeverity). Returns
    /// <paramref name="context"/> unchanged when no profiles are requested or no provider is registered.
    /// </summary>
    private KyrolusValidationContext ApplyProfiles(KyrolusValidationContext context)
    {
        if (context.Profiles is not { Count: > 0 }) return context;

        var provider = serviceProvider.GetService<IKyrolusValidationProfileProvider>();
        if (provider is null) return context;

        var accumulator = new ProfileAccumulator(context);

        foreach (var profileName in context.Profiles)
            if (provider.TryGetProfile(profileName, out var profileContext))
                accumulator.Apply(profileContext);

        return accumulator.Build(context);
    }

    private sealed class ProfileAccumulator
    {
        // A List (not a HashSet) preserves the deterministic order in which RuleSets/Groups were first declared -
        // across the base context and every merged profile - instead of an unspecified HashSet enumeration order.
        // That order matters because ResolveActiveRuleSet's "no exact match" fallback picks contextRuleSets.First().
        private readonly List<string> ruleSets;
        private readonly List<string> groups;
        private readonly bool hasRuleSets;
        private readonly bool hasGroups;
        private KyrolusValidationSeverity? minimumSeverity;

        public ProfileAccumulator(KyrolusValidationContext context)
        {
            ruleSets = CreateOrderPreservingSet(context.RuleSets, out hasRuleSets);
            groups = CreateOrderPreservingSet(context.Groups, out hasGroups);
            minimumSeverity = context.MinimumSeverity;
        }

        public void Apply(KyrolusValidationContext profileContext)
        {
            AddRange(ruleSets, profileContext.RuleSets);
            AddRange(groups, profileContext.Groups);
            minimumSeverity = MaxSeverity(minimumSeverity, profileContext.MinimumSeverity);
        }

        public KyrolusValidationContext Build(KyrolusValidationContext baseContext)
            => baseContext with
            {
                RuleSets = ruleSets.Count > 0 || hasRuleSets ? [.. ruleSets] : baseContext.RuleSets,
                Groups = groups.Count > 0 || hasGroups ? [.. groups] : baseContext.Groups,
                MinimumSeverity = minimumSeverity
            };


        private static List<string> CreateOrderPreservingSet(
            IReadOnlyCollection<string>? values,
            out bool hasValues)
        {
            if (values is { Count: > 0 })
            {
                hasValues = true;
                return [.. values.Distinct(StringComparer.OrdinalIgnoreCase)];
            }

            hasValues = false;
            return [];
        }

        private static void AddRange(List<string> target, IReadOnlyCollection<string>? values)
        {
            if (values is not { Count: > 0 }) return;

            foreach (var value in values)
                if (!target.Contains(value, StringComparer.OrdinalIgnoreCase))
                    target.Add(value);
        }

        private static KyrolusValidationSeverity? MaxSeverity(
            KyrolusValidationSeverity? current,
            KyrolusValidationSeverity? incoming)
        {
            if (incoming is null) return current;
            if (current is null) return incoming;

            return (KyrolusValidationSeverity)Math.Max((int)current.Value, (int)incoming.Value);
        }
    }

    /// <summary>
    /// Invokes <see cref="IKyrolusValidationHook.OnBeforeAsync"/> for every registered global hook, then every
    /// <see cref="IKyrolusValidationHook{TRequest}"/> registered specifically for <typeparamref name="TRequest"/>.
    /// Within each group, hooks run in ascending order (see <see cref="ResolveHookOrder(object, int)"/>); LINQ's
    /// <c>OrderBy</c> is a stable sort, so hooks that tie (the default for both) keep their registration order.
    /// </summary>
    private async ValueTask RunBeforeHooks<TRequest>(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var globalHooks = serviceProvider.GetServices<IKyrolusValidationHook>()
            .OrderBy(hook => ResolveHookOrder(hook, hook.Order));
        foreach (var hook in globalHooks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await hook.OnBeforeAsync(request, context, cancellationToken).ConfigureAwait(false);
        }

        var requestHooks = serviceProvider.GetServices<IKyrolusValidationHook<TRequest>>()
            .OrderBy(hook => ResolveHookOrder(hook, hook.Order));
        foreach (var hook in requestHooks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await hook.OnBeforeAsync(request, context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Invokes <see cref="IKyrolusValidationHook.OnAfterAsync"/> for every registered global hook, then every
    /// <see cref="IKyrolusValidationHook{TRequest}"/> registered specifically for <typeparamref name="TRequest"/>,
    /// in the same relative order as <see cref="RunBeforeHooks"/> (this is not a LIFO unwind). Runs even when
    /// validation threw, so hooks that opened a resource in <c>OnBeforeAsync</c> (a tracing <c>Activity</c>, a
    /// metrics <c>Stopwatch</c>) always get to close it.
    /// </summary>
    private async ValueTask RunAfterHooks<TRequest>(
        TRequest request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken)
    {
        var globalHooks = serviceProvider.GetServices<IKyrolusValidationHook>()
            .OrderBy(hook => ResolveHookOrder(hook, hook.Order));
        foreach (var hook in globalHooks)
            await hook.OnAfterAsync(request, context, failures, cancellationToken).ConfigureAwait(false);

        var requestHooks = serviceProvider.GetServices<IKyrolusValidationHook<TRequest>>()
            .OrderBy(hook => ResolveHookOrder(hook, hook.Order));
        foreach (var hook in requestHooks)
            await hook.OnAfterAsync(request, context, failures, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A <see cref="KyrolusValidationHookOrderAttribute"/> resolved via <see cref="hookOrderLookup"/> (when one
    /// is registered) takes precedence over <paramref name="declaredOrder"/> - the hook's own
    /// <see cref="IKyrolusValidationHook.Order"/>/<see cref="IKyrolusValidationHook{TRequest}.Order"/> - so a
    /// consuming project can decorate a hook with the attribute instead of overriding the property.
    /// </summary>
    private int ResolveHookOrder(object hook, int declaredOrder) =>
        hookOrderLookup?.TryGetOrder(hook.GetType()) ?? declaredOrder;

    /// <summary>
    /// Localizes a failure's error message. The translation key is
    /// <see cref="KyrolusValidationFailure.MessageKey"/>, falling back to
    /// <see cref="KyrolusValidationFailure.ErrorCode"/> then <see cref="KyrolusValidationFailure.ErrorMessage"/>.
    /// The failure itself is passed as the interpolation source, so a template like
    /// "Value {AttemptedValue} for {PropertyName} is invalid" is filled in automatically.
    /// </summary>
    private static string LocalizeFailure(IKyrolusLocalizer localizer, KyrolusValidationFailure failure)
    {
        var key = failure.MessageKey ?? failure.ErrorCode ?? failure.ErrorMessage;
        var arguments = new Dictionary<string, object?>
        {
            [nameof(failure.PropertyName)] = failure.PropertyName,
            [nameof(failure.ErrorMessage)] = failure.ErrorMessage,
            [nameof(failure.ErrorCode)] = failure.ErrorCode,
            [nameof(failure.Severity)] = failure.Severity,
            [nameof(failure.RuleSet)] = failure.RuleSet,
            [nameof(failure.MessageKey)] = failure.MessageKey,
            [nameof(failure.AttemptedValue)] = failure.AttemptedValue,
            [nameof(failure.FieldPath)] = failure.FieldPath,
        };
        return localizer.GetStringOrDefault(key, arguments, failure.ErrorMessage);
    }

    /// <summary>
    /// Fills in <see cref="KyrolusValidationDefaults.DefaultRuleSet"/>/<see cref="KyrolusValidationDefaults.DefaultGroup"/>
    /// for a failure with no explicit RuleSet/Groups, so an untagged rule participates in RuleSet/Group filtering
    /// (<see cref="ApplyFilters"/>) the same way an explicitly-scoped one does.
    /// </summary>
    private static KyrolusValidationFailure NormalizeFailure(KyrolusValidationFailure failure)
    {
        var ruleSet = string.IsNullOrWhiteSpace(failure.RuleSet) ? KyrolusValidationDefaults.DefaultRuleSet : failure.RuleSet;
        var groups = failure.Groups is { Count: > 0 } ? failure.Groups : [KyrolusValidationDefaults.DefaultGroup];
        return failure with { RuleSet = ruleSet, Groups = groups };
    }

    /// <summary>
    /// Applies the optional <see cref="IKyrolusValidationErrorCodeMapper"/>/<see cref="IKyrolusValidationFieldPathMapper"/>
    /// to each failure's <see cref="KyrolusValidationFailure.ErrorCode"/>/<see cref="KyrolusValidationFailure.FieldPath"/>.
    /// A mapper returning a blank value leaves the original value in place rather than blanking it out.
    /// </summary>
    private KyrolusValidationFailure[] ApplyMappings(
        KyrolusValidationFailure[] failures,
        KyrolusValidationContext context)
    {
        if (errorCodeMapper is null && fieldPathMapper is null) return failures;

        var mapped = new KyrolusValidationFailure[failures.Length];
        for (var index = 0; index < failures.Length; index++)
        {
            var failure = failures[index];
            var mappedCode = errorCodeMapper?.MapErrorCode(failure, context);
            var mappedPath = fieldPathMapper?.MapFieldPath(failure, context);

            if (string.IsNullOrWhiteSpace(mappedCode)) mappedCode = failure.ErrorCode;

            if (string.IsNullOrWhiteSpace(mappedPath)) mappedPath = failure.FieldPath;

            mapped[index] = failure with { ErrorCode = mappedCode, FieldPath = mappedPath };
        }

        return mapped;
    }

    /// <summary>
    /// Resolves the cache entry (key, mode, TTL) for <paramref name="request"/> via the registered
    /// <see cref="IKyrolusValidationCacheKeyProvider"/>. Returns <see langword="null"/> - meaning "don't cache
    /// this call" - when no provider is registered, the request is <see langword="null"/>, or the provider
    /// declines it (e.g. it doesn't implement <see cref="IKyrolusValidationCacheable"/>).
    /// </summary>
    private KyrolusValidationCacheEntry? ResolveCacheEntry<TRequest>(
        TRequest request,
        KyrolusValidationContext context)
    {
        if (cacheKeyProvider is null || request is null) return null;

        return cacheKeyProvider.GetCacheEntry(request, context);
    }

    /// <summary>
    /// The shared tail of the "no validators registered" and "every validator passed" branches in
    /// <see cref="ValidateAsync{TRequest}(TRequest, KyrolusValidationContext, CancellationToken)"/> - both mean
    /// the same outcome (an empty failure list), so both cache it and run the After hooks the same way.
    /// </summary>
    private async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ReturnEmptyResultAsync<TRequest>(
        TRequest request,
        KyrolusValidationContext effectiveContext,
        KyrolusValidationCacheEntry? cacheEntry,
        CancellationToken cancellationToken)
    {
        var empty = Array.Empty<KyrolusValidationFailure>();
        await TryStoreCache(cacheEntry, empty, cancellationToken).ConfigureAwait(false);
        await RunAfterHooks(request, effectiveContext, empty, cancellationToken).ConfigureAwait(false);
        return empty;
    }

    /// <summary>
    /// Stores <paramref name="failures"/> under <paramref name="cacheEntry"/>'s key when its
    /// <see cref="KyrolusValidationCacheMode"/> permits caching this outcome (success vs. failure). A no-op when
    /// <paramref name="cacheEntry"/> or the cache store is <see langword="null"/>.
    /// </summary>
    private async ValueTask TryStoreCache(
        KyrolusValidationCacheEntry? cacheEntry,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken,
        TimeSpan? ttlOverride = null)
    {
        if (cacheEntry is null || cacheStore is null || !ShouldCache(cacheEntry.Mode, failures)) return;
        var ttl = ttlOverride ?? cacheEntry.Ttl;
        await cacheStore.SetAsync(cacheEntry.Key, failures, ttl, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the TTL used to cache a <em>failed</em> (negative) validation result: <paramref name="request"/>'s
    /// own <see cref="IKyrolusValidationNegativeCacheable.NegativeCacheTtl"/> when it opts in, otherwise the
    /// shorter <see cref="KyrolusValidationCacheDefaults.NegativeTtl"/> (30s vs. the 5-minute default used for a
    /// passing result). Standard "negative caching" reasoning (as in DNS NXDOMAIN or HTTP 404 caching): a failure
    /// is cached for less time than a success because whatever caused it (a uniqueness conflict, a missing
    /// referenced entity, ...) is more likely to be resolved soon than a "this is valid" result is to become invalid.
    /// </summary>
    private static TimeSpan ResolveNegativeTtl<TRequest>(TRequest request)
    {
        if (request is IKyrolusValidationNegativeCacheable negative
            && negative.NegativeCacheTtl is { } ttl
            && ttl > TimeSpan.Zero)
            return ttl;

        return KyrolusValidationCacheDefaults.NegativeTtl;
    }

    private static bool ShouldCache(
        KyrolusValidationCacheMode mode,
        IReadOnlyList<KyrolusValidationFailure> failures)
    {
        return mode switch
        {
            KyrolusValidationCacheMode.SuccessOnly => failures.Count == 0,
            KyrolusValidationCacheMode.FailuresOnly => failures.Count > 0,
            KyrolusValidationCacheMode.All => true,
            _ => false
        };
    }

    private static IReadOnlyList<KyrolusValidationFailure> ApplyFilters(
        IReadOnlyList<KyrolusValidationFailure> failures,
        KyrolusValidationContext context)
    {
        IEnumerable<KyrolusValidationFailure> query = failures;

        if (context.MinimumSeverity is not null)
            query = query.Where(failure => failure.Severity >= context.MinimumSeverity.Value);

        if (context.RuleSets is { Count: > 0 })
            query = query.Where(failure => context.RuleSets.Contains(failure.RuleSet ?? KyrolusValidationDefaults.DefaultRuleSet, StringComparer.OrdinalIgnoreCase));

        if (context.Groups is { Count: > 0 })
            query = query.Where(failure =>
                (failure.Groups is { Count: > 0 } ? failure.Groups : [KyrolusValidationDefaults.DefaultGroup])
                    .Any(g => context.Groups.Contains(g, StringComparer.OrdinalIgnoreCase)));

        return [.. query];
    }
}
