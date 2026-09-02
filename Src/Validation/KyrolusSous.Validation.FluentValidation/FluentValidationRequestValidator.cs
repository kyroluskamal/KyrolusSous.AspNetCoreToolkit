namespace KyrolusSous.Validation.FluentValidation;

/// <summary>
/// Adapter integrating FluentValidation's <see cref="IValidator{T}"/> with <see cref="IKyrolusRequestValidatorWithContext{TRequest}"/>.
/// Resolves registered FluentValidation validators from DI, maps RuleSets, and translates failures to <see cref="KyrolusValidationFailure"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request being validated.</typeparam>
/// <example>
/// <code>
/// // Register FluentValidation in DI
/// services.AddValidatorsFromAssemblyContaining&lt;CreateUserValidator&gt;();
/// services.AddKyrolusFluentValidationAdapter();
/// 
/// // Execute through the unified engine
/// var failures = await engine.ValidateAsync(createUserRequest, ct);
/// </code>
/// </example>
public sealed class FluentValidationRequestValidator<TRequest>(IServiceProvider serviceProvider)
    : IKyrolusRequestValidatorWithContext<TRequest>
{
    /// <summary>Every <see cref="IValidator{T}"/> registered for <typeparamref name="TRequest"/>, resolved once at construction. All of them run on every <see cref="ValidateAsync(TRequest, CancellationToken)"/> call.</summary>
    private readonly IReadOnlyList<IValidator<TRequest>> _validators =
        serviceProvider?.GetServices<IValidator<TRequest>>().Where(v => v is not null).ToArray() ?? [];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request, CancellationToken cancellationToken = default)
            => ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_validators.Count == 0)
            return [];

        var validationContext = CreateValidationContext(request, context);
        var allFailures = new List<KyrolusValidationFailure>();

        foreach (var validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await validator.ValidateAsync(validationContext, cancellationToken).ConfigureAwait(false);
            if (result is null || result.IsValid)
                continue;

            var descriptor = validator.CreateDescriptor();
            foreach (var error in result.Errors.Where(error => error is not null))
                allFailures.Add(MapToFailure(error, context, descriptor));
        }

        return allFailures;
    }

    /// <summary>
    /// Builds a FluentValidation <see cref="ValidationContext{T}"/> for <paramref name="request"/>, translating
    /// <see cref="KyrolusValidationContext.RuleSets"/> into FluentValidation's own RuleSet execution options -
    /// <c>IncludeAllRuleSets()</c> for a wildcard <c>"*"</c>, or <c>IncludeRuleSets(...)</c> for a specific list.
    /// Falls back to FluentValidation's own default execution (no <see cref="ValidationContext{T}.CreateWithOptions"/>)
    /// when the context requests no specific RuleSets.
    /// </summary>
    private static ValidationContext<TRequest> CreateValidationContext(TRequest request, KyrolusValidationContext context)
    {
        if (context.RuleSets is not { Count: > 0 })
            return new ValidationContext<TRequest>(request);

        return ValidationContext<TRequest>.CreateWithOptions(request, options =>
        {
            if (context.RuleSets.Contains("*"))
                options.IncludeAllRuleSets();
            else
                options.IncludeRuleSets([.. context.RuleSets]);
        });
    }

    /// <summary>
    /// Translates one FluentValidation <see cref="ValidationFailure"/> into a <see cref="KyrolusValidationFailure"/>,
    /// resolving its RuleSet via <paramref name="descriptor"/> (see <see cref="ResolveDeclaredRuleSets"/>) and its
    /// Groups/Metadata from <see cref="ValidationFailure.CustomState"/> (see <see cref="ResolveGroups"/>/<see cref="BuildMetadata"/>).
    /// </summary>
    private static KyrolusValidationFailure MapToFailure(ValidationFailure error, KyrolusValidationContext context, IValidatorDescriptor descriptor)
    {
        var metadata = BuildMetadata(error);
        var groups = ResolveGroups(error);

        // The failure carries no direct link back to the FluentValidation IValidationRule that produced it, only
        // its PropertyName - so the RuleSet(s) actually declared for that member are looked up via the
        // descriptor and resolved against the requested context, exactly like the Fluent DSL and the
        // DataAnnotations validators do. Previously this always stamped context.RuleSets.First() regardless of
        // which RuleSet the property actually belongs to, mislabeling failures whenever multiple RuleSets were
        // requested simultaneously.
        var declaredRuleSets = ResolveDeclaredRuleSets(descriptor, error.PropertyName);
        var ruleSet = declaredRuleSets.Length > 0
            ? KyrolusValidationScopeResolver.ResolveActiveRuleSet(declaredRuleSets, context.RuleSets)
            : null;

        return new KyrolusValidationFailure(
            error.PropertyName,
            error.ErrorMessage,
            error.ErrorCode,
            MapSeverity(error.Severity),
            RuleSet: ruleSet,
            MessageKey: string.IsNullOrWhiteSpace(error.ErrorCode) ? null : error.ErrorCode,
            AttemptedValue: error.AttemptedValue,
            Metadata: metadata,
            Groups: groups.Count > 0 ? groups : null);
    }

    /// <summary>
    /// Looks up every RuleSet name declared on any FluentValidation rule for <paramref name="propertyName"/>, via
    /// <see cref="IValidatorDescriptor.GetRulesForMember"/> - the closest available substitute for a direct link
    /// from the failure back to the specific rule that produced it, since <see cref="ValidationFailure"/> itself
    /// doesn't carry one.
    /// </summary>
    private static string[] ResolveDeclaredRuleSets(IValidatorDescriptor descriptor, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return [];

        return descriptor.GetRulesForMember(propertyName)
            .SelectMany(rule => rule.RuleSets ?? [])
            .Where(ruleSet => !string.IsNullOrWhiteSpace(ruleSet))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Maps FluentValidation's <see cref="Severity"/> to <see cref="KyrolusValidationSeverity"/>; anything other than Info/Warning (including FluentValidation's own default) maps to <see cref="KyrolusValidationSeverity.Error"/>.</summary>
    private static KyrolusValidationSeverity MapSeverity(Severity severity)
    => severity switch
    {
        Severity.Info => KyrolusValidationSeverity.Info,
        Severity.Warning => KyrolusValidationSeverity.Warning,
        _ => KyrolusValidationSeverity.Error
    };

    /// <summary>
    /// Builds the failure's Metadata dictionary from <paramref name="error"/>'s <see cref="ValidationFailure.FormattedMessagePlaceholderValues"/>
    /// (the named values FluentValidation substituted into the error message) plus its raw <see cref="ValidationFailure.CustomState"/>
    /// under the <c>"customState"</c> key - unless that state is a <see cref="KyrolusValidationGroup"/>, which is
    /// Group data handled separately by <see cref="ResolveGroups"/>, not metadata. Returns <see langword="null"/> when there's nothing to report.
    /// </summary>
    private static IReadOnlyDictionary<string, object?>? BuildMetadata(ValidationFailure error)
    {
        if (error.CustomState is null && error.FormattedMessagePlaceholderValues is not { Count: > 0 })
            return null;

        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (error.FormattedMessagePlaceholderValues is { Count: > 0 })
            foreach (var (k, v) in error.FormattedMessagePlaceholderValues)
                metadata[k] = v;
        if (error.CustomState is not null and not KyrolusValidationGroup)
            metadata["customState"] = error.CustomState;
        return metadata;
    }

    /// <summary>
    /// Extracts Group tags from <paramref name="error"/>'s <see cref="ValidationFailure.CustomState"/>, supporting
    /// three shapes set via FluentValidation's <c>.WithState(...)</c>: a <see cref="KyrolusValidationGroup"/>
    /// instance, a single group name string, or a dictionary (see <see cref="ResolveGroupsFromDictionary"/>).
    /// Returns an empty list when <see cref="ValidationFailure.CustomState"/> is <see langword="null"/> or none of these shapes match.
    /// </summary>
    private static IReadOnlyList<string> ResolveGroups(ValidationFailure error)
    {
        if (error.CustomState is KyrolusValidationGroup group)
            return group.Names;

        if (error.CustomState is string groupName && !string.IsNullOrWhiteSpace(groupName))
            return [groupName];

        if (error.CustomState is IDictionary<string, object?> dict)
            return ResolveGroupsFromDictionary(dict);

        return [];
    }

    /// <summary>
    /// Extracts Group tags from a dictionary-shaped <see cref="ValidationFailure.CustomState"/> - a <c>"groups"</c>
    /// entry holding either a string collection or a single string, falling back to a singular <c>"group"</c>
    /// string entry. Returns an empty list when neither key is present in a recognized shape.
    /// </summary>
    private static IReadOnlyList<string> ResolveGroupsFromDictionary(IDictionary<string, object?> dict)
    {
        if (dict.TryGetValue("groups", out var groupsObj))
        {
            if (groupsObj is IEnumerable<string> stringEnum)
                return [.. stringEnum.Where(g => !string.IsNullOrWhiteSpace(g))];

            if (groupsObj is string singleGroup && !string.IsNullOrWhiteSpace(singleGroup))
                return [singleGroup];
        }

        if (dict.TryGetValue("group", out var singleGroupObj) &&
            singleGroupObj is string gName && !string.IsNullOrWhiteSpace(gName))
            return [gName];

        return [];
    }
}
