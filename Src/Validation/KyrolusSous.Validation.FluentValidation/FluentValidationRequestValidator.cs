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
    private readonly IReadOnlyList<IValidator<TRequest>> _validators =
        serviceProvider?.GetServices<IValidator<TRequest>>().Where(v => v is not null).ToArray() ?? [];

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_validators.Count == 0)
        {
            return [];
        }

        var validationContext = CreateValidationContext(request, context);
        var allFailures = new List<KyrolusValidationFailure>();

        foreach (var validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await validator.ValidateAsync(validationContext, cancellationToken).ConfigureAwait(false);
            if (result is null || result.IsValid)
            {
                continue;
            }

            foreach (var error in result.Errors.Where(error => error is not null))
            {
                allFailures.Add(MapToFailure(error, context));
            }
        }

        return allFailures;
    }

    private static ValidationContext<TRequest> CreateValidationContext(TRequest request, KyrolusValidationContext context)
    {
        if (context.RuleSets is not { Count: > 0 })
        {
            return new ValidationContext<TRequest>(request);
        }

        return ValidationContext<TRequest>.CreateWithOptions(request, options =>
        {
            if (context.RuleSets.Contains("*"))
            {
                options.IncludeAllRuleSets();
            }
            else
            {
                options.IncludeRuleSets(context.RuleSets.ToArray());
            }
        });
    }

    private static KyrolusValidationFailure MapToFailure(ValidationFailure error, KyrolusValidationContext context)
    {
        var metadata = BuildMetadata(error);
        var groups = ResolveGroups(error);
        var ruleSet = context.RuleSets is { Count: > 0 } ? context.RuleSets.First() : null;

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

    private static KyrolusValidationSeverity MapSeverity(Severity severity)
    {
        return severity switch
        {
            Severity.Info => KyrolusValidationSeverity.Info,
            Severity.Warning => KyrolusValidationSeverity.Warning,
            _ => KyrolusValidationSeverity.Error
        };
    }

    private static IReadOnlyDictionary<string, object?>? BuildMetadata(ValidationFailure error)
    {
        if (error.CustomState is null && error.FormattedMessagePlaceholderValues is not { Count: > 0 })
        {
            return null;
        }

        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (error.FormattedMessagePlaceholderValues is { Count: > 0 })
        {
            foreach (var (k, v) in error.FormattedMessagePlaceholderValues)
            {
                metadata[k] = v;
            }
        }

        if (error.CustomState is not null and not KyrolusValidationGroup)
        {
            metadata["customState"] = error.CustomState;
        }

        return metadata;
    }

    private static IReadOnlyList<string> ResolveGroups(ValidationFailure error)
    {
        if (error.CustomState is KyrolusValidationGroup group)
        {
            return group.Names;
        }

        if (error.CustomState is string groupName && !string.IsNullOrWhiteSpace(groupName))
        {
            return [groupName];
        }

        if (error.CustomState is IDictionary<string, object?> dict)
        {
            return ResolveGroupsFromDictionary(dict);
        }

        return [];
    }

    private static IReadOnlyList<string> ResolveGroupsFromDictionary(IDictionary<string, object?> dict)
    {
        if (dict.TryGetValue("groups", out var groupsObj))
        {
            if (groupsObj is IEnumerable<string> stringEnum)
            {
                return stringEnum.Where(g => !string.IsNullOrWhiteSpace(g)).ToArray();
            }

            if (groupsObj is string singleGroup && !string.IsNullOrWhiteSpace(singleGroup))
            {
                return [singleGroup];
            }
        }

        if (dict.TryGetValue("group", out var singleGroupObj) &&
            singleGroupObj is string gName &&
            !string.IsNullOrWhiteSpace(gName))
        {
            return [gName];
        }

        return [];
    }
}
