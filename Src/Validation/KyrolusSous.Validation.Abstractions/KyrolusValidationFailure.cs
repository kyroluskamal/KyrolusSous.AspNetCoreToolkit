namespace KyrolusSous.Validation.Abstractions;

public enum KyrolusValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed record KyrolusValidationFailure(
    string PropertyName,
    string ErrorMessage,
    string? ErrorCode = null,
    KyrolusValidationSeverity Severity = KyrolusValidationSeverity.Error,
    string? RuleSet = null,
    string? Group = null,
    string? MessageKey = null,
    object? AttemptedValue = null,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    string? FieldPath = null);
