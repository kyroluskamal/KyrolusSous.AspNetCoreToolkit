namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusValidationEngine(
    IServiceProvider serviceProvider,
    IKyrolusLocalizer? localizer = null,
    IKyrolusValidationCacheStore? cacheStore = null,
    IKyrolusValidationCacheKeyProvider? cacheKeyProvider = null,
    IKyrolusValidationErrorCodeMapper? errorCodeMapper = null,
    IKyrolusValidationFieldPathMapper? fieldPathMapper = null) : IKyrolusValidationEngine
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IKyrolusLocalizer? localizer = localizer;
    private readonly IKyrolusValidationCacheStore? cacheStore = cacheStore;
    private readonly IKyrolusValidationCacheKeyProvider? cacheKeyProvider = cacheKeyProvider;
    private readonly IKyrolusValidationErrorCodeMapper? errorCodeMapper = errorCodeMapper;
    private readonly IKyrolusValidationFieldPathMapper? fieldPathMapper = fieldPathMapper;

    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default)
    => await ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken).ConfigureAwait(false);

    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync<TRequest>(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveContext = ApplyProfiles(context);

        await RunBeforeHooks(request, effectiveContext, cancellationToken).ConfigureAwait(false);

        var cacheEntry = ResolveCacheEntry(request, effectiveContext);
        if (cacheEntry is not null
            && cacheStore is not null
            && cacheStore.TryGet(cacheEntry.Key, out var cached))
        {
            var cachedResult = localizer is null
                ? cached
                : [.. cached.Select(failure => failure with { ErrorMessage = LocalizeFailure(localizer, failure) })];

            await RunAfterHooks(request, effectiveContext, cachedResult, cancellationToken).ConfigureAwait(false);
            return cachedResult;
        }

        var validators = serviceProvider.GetServices<IKyrolusRequestValidator<TRequest>>().ToArray();
        if (validators.Length == 0)
        {
            var empty = Array.Empty<KyrolusValidationFailure>();
            TryStoreCache(cacheEntry, empty, ResolveNegativeTtl(request));
            await RunAfterHooks(request, effectiveContext, empty, cancellationToken).ConfigureAwait(false);
            return empty;
        }

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
        {
            var empty = Array.Empty<KyrolusValidationFailure>();
            TryStoreCache(cacheEntry, empty, ResolveNegativeTtl(request));
            await RunAfterHooks(request, effectiveContext, empty, cancellationToken).ConfigureAwait(false);
            return empty;
        }

        var normalized = failures.Select(NormalizeFailure).ToArray();
        var mapped = ApplyMappings(normalized, effectiveContext);
        var filtered = ApplyFilters(mapped, effectiveContext);
        TryStoreCache(cacheEntry, filtered);

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

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond>(
        TFirst first,
        TSecond second,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second);
        return ValidateAsync(composite, KyrolusValidationContext.Default, cancellationToken);
    }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond>(
        TFirst first,
        TSecond second,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second);
        return ValidateAsync(composite, context, cancellationToken);
    }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird>(
        TFirst first,
        TSecond second,
        TThird third,
        CancellationToken cancellationToken = default)
    {
        var composite = KyrolusValidationComposite.Create(first, second, third);
        return ValidateAsync(composite, KyrolusValidationContext.Default, cancellationToken);
    }

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
        private readonly HashSet<string> ruleSets;
        private readonly HashSet<string> groups;
        private readonly bool hasRuleSets;
        private readonly bool hasGroups;
        private KyrolusValidationSeverity? minimumSeverity;

        public ProfileAccumulator(KyrolusValidationContext context)
        {
            ruleSets = CreateSet(context.RuleSets, out hasRuleSets);
            groups = CreateSet(context.Groups, out hasGroups);
            minimumSeverity = context.MinimumSeverity;
        }

        public void Apply(KyrolusValidationContext profileContext)
        {
            AddRange(ruleSets, profileContext.RuleSets);
            AddRange(groups, profileContext.Groups);
            minimumSeverity = MaxSeverity(minimumSeverity, profileContext.MinimumSeverity);
        }

        public KyrolusValidationContext Build(KyrolusValidationContext baseContext)
        {
            return baseContext with
            {
                RuleSets = ruleSets.Count > 0 || hasRuleSets ? [.. ruleSets] : baseContext.RuleSets,
                Groups = groups.Count > 0 || hasGroups ? [.. groups] : baseContext.Groups,
                MinimumSeverity = minimumSeverity
            };
        }

        private static HashSet<string> CreateSet(
            IReadOnlyCollection<string>? values,
            out bool hasValues)
        {
            if (values is { Count: > 0 })
            {
                hasValues = true;
                return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
            }

            hasValues = false;
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private static void AddRange(HashSet<string> target, IReadOnlyCollection<string>? values)
        {
            if (values is { Count: > 0 }) target.UnionWith(values);
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

    private async ValueTask RunBeforeHooks<TRequest>(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var hook in serviceProvider.GetServices<IKyrolusValidationHook>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await hook.OnBeforeAsync(request, context, cancellationToken).ConfigureAwait(false);
        }

        foreach (var hook in serviceProvider.GetServices<IKyrolusValidationHook<TRequest>>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await hook.OnBeforeAsync(request, context, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask RunAfterHooks<TRequest>(
        TRequest request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken)
    {
        foreach (var hook in serviceProvider.GetServices<IKyrolusValidationHook>())
            await hook.OnAfterAsync(request, context, failures, cancellationToken).ConfigureAwait(false);

        foreach (var hook in serviceProvider.GetServices<IKyrolusValidationHook<TRequest>>())
            await hook.OnAfterAsync(request, context, failures, cancellationToken).ConfigureAwait(false);
    }

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
        return localizer.GetStringOrDefault(key, failure, failure.ErrorMessage);
    }

    private static KyrolusValidationFailure NormalizeFailure(KyrolusValidationFailure failure)
    {
        var ruleSet = string.IsNullOrWhiteSpace(failure.RuleSet) ? KyrolusValidationDefaults.DefaultRuleSet : failure.RuleSet;
        var groups = failure.Groups is { Count: > 0 } ? failure.Groups : [KyrolusValidationDefaults.DefaultGroup];
        return failure with { RuleSet = ruleSet, Groups = groups };
    }

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

    private KyrolusValidationCacheEntry? ResolveCacheEntry<TRequest>(
        TRequest request,
        KyrolusValidationContext context)
    {
        if (cacheKeyProvider is null|| request is null) return null;

        return cacheKeyProvider.GetCacheEntry(request!, context);
    }

    private void TryStoreCache(
        KyrolusValidationCacheEntry? cacheEntry,
        IReadOnlyList<KyrolusValidationFailure> failures,
        TimeSpan? ttlOverride = null)
    {
        if (cacheEntry is null || cacheStore is null || !ShouldCache(cacheEntry.Mode, failures)) return;
        var ttl = ttlOverride ?? cacheEntry.Ttl;
        cacheStore.Set(cacheEntry.Key, failures, ttl);
    }

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
