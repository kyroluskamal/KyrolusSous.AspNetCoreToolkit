namespace KyrolusSous.Validation.FluentValidation;

public sealed class FluentValidationRequestValidator<TRequest>(IServiceProvider serviceProvider)
    : IKyrolusRequestValidatorWithContext<TRequest>
{
    private readonly IReadOnlyList<IValidator<TRequest>> _validators =
        serviceProvider?.GetServices<IValidator<TRequest>>().Where(v => v is not null).ToArray() ?? [];

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_validators.Count == 0)
        {
            return [];
        }

        var validationContext = context.RuleSets is { Count: > 0 }
            ? ValidationContext<TRequest>.CreateWithOptions(request, options =>
            {
                if (context.RuleSets.Contains("*"))
                {
                    options.IncludeAllRuleSets();
                }
                else
                {
                    options.IncludeRuleSets(context.RuleSets.ToArray());
                }
            })
            : new ValidationContext<TRequest>(request);

        var allFailures = new List<KyrolusValidationFailure>();

        foreach (var validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await validator.ValidateAsync(validationContext, cancellationToken).ConfigureAwait(false);
            if (result is null || result.IsValid)
            {
                continue;
            }

            var failures = result.Errors
                .Where(error => error is not null)
                .Select(error =>
                {
                    var metadata = BuildMetadata(error);
                    var group = ResolveGroup(error);
                    var ruleSet = context.RuleSets is { Count: > 0 } ? context.RuleSets.First() : null;
                    return new KyrolusValidationFailure(
                        error.PropertyName,
                        error.ErrorMessage,
                        error.ErrorCode,
                        MapSeverity(error.Severity),
                        RuleSet: ruleSet,
                        Group: group,
                        MessageKey: string.IsNullOrWhiteSpace(error.ErrorCode) ? null : error.ErrorCode,
                        AttemptedValue: error.AttemptedValue,
                        Metadata: metadata);
                });

            allFailures.AddRange(failures);
        }

        return allFailures;
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
        Dictionary<string, object?>? metadata = null;

        if (error.FormattedMessagePlaceholderValues is { Count: > 0 })
        {
            metadata = new Dictionary<string, object?>(error.FormattedMessagePlaceholderValues);
        }

        if (error.CustomState is not null)
        {
            metadata ??= [];
            metadata["customState"] = error.CustomState;
        }

        return metadata;
    }

    private static string? ResolveGroup(ValidationFailure error)
    {
        if (error.CustomState is KyrolusValidationGroup group)
        {
            return group.Name;
        }

        if (error.CustomState is string name && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (error.CustomState is IReadOnlyDictionary<string, object?> map
            && map.TryGetValue("group", out var value)
            && value is not null)
        {
            return value.ToString();
        }

        return null;
    }
}
