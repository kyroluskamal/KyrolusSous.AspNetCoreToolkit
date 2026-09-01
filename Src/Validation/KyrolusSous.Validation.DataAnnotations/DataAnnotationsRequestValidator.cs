using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.DataAnnotations;

public sealed class DataAnnotationsRequestValidator<TRequest>(IServiceProvider? serviceProvider = null)
    : IKyrolusRequestValidatorWithContext<TRequest>
{
    // Property-level [KyrolusValidationScope] lookups are cached per (declaring member) since the metadata is
    // static for the process lifetime and TryValidateObject already reflects over the same members every call.
    private static readonly ConcurrentDictionary<MemberInfo, KyrolusValidationScopeAttribute?> ScopeCache = new();

    private readonly IServiceProvider? _serviceProvider = serviceProvider;

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request, CancellationToken cancellationToken = default) => ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken);

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request is null)
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(
                [new KyrolusValidationFailure(string.Empty, "Request is required.")]);

        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(request, serviceProvider: _serviceProvider, items: null);
        if (context is not null)
            validationContext.Items[nameof(KyrolusValidationContext)] = context;

        var isValid = Validator.TryValidateObject(request, validationContext, results, validateAllProperties: true);

        if (isValid)
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>([]);

        var failures = results
            .SelectMany(result =>
            {
                var error = result.ErrorMessage ?? "Validation error.";
                if (result.MemberNames is null || !result.MemberNames.Any())
                    return [new KyrolusValidationFailure(string.Empty, error)];

                return result.MemberNames
                    .Select(member => BuildFailure(member, error, context))
                    .OfType<KyrolusValidationFailure>();
            })
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(failures);
    }

    /// <summary>
    /// Builds the failure for a failed member, or returns null to drop it entirely when a
    /// <see cref="KyrolusValidationScopeAttribute"/> on the property doesn't match the requested context - mirrors
    /// the Fluent DSL, where a rule scoped to a RuleSet/Group the caller didn't ask for never executes at all
    /// rather than running and getting mislabeled with whatever RuleSet the caller happened to request.
    /// </summary>
    private static KyrolusValidationFailure? BuildFailure(string member, string error, KyrolusValidationContext? context)
    {
        var scope = ResolveScope(member);
        var ruleSets = scope?.RuleSets ?? [];
        var groups = scope?.Groups ?? [];

        // Gate applies uniformly whether or not the property is tagged: an untagged property behaves like a
        // Fluent rule with no RuleSets/Groups attached, which only runs for the default scope.
        if (!KyrolusValidationScopeResolver.ShouldExecute(context?.RuleSets, ruleSets, context?.Groups, groups))
            return null;

        if (ruleSets.Length == 0 && groups.Length == 0)
            return new KyrolusValidationFailure(member, error);

        var ruleSet = ruleSets.Length > 0
            ? KyrolusValidationScopeResolver.ResolveActiveRuleSet(ruleSets, context?.RuleSets)
            : null;
        var groupsResult = groups.Length > 0 ? groups : null;
        return new KyrolusValidationFailure(member, error, RuleSet: ruleSet, Groups: groupsResult);
    }

    private static KyrolusValidationScopeAttribute? ResolveScope(string member)
    {
        var property = typeof(TRequest).GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
            return null;

        return ScopeCache.GetOrAdd(property, static p => p.GetCustomAttribute<KyrolusValidationScopeAttribute>());
    }
}
