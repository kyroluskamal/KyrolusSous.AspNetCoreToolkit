namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Represents a single validation failure resulting from an unsatisfied validation rule.
/// </summary>
/// <param name="PropertyName">The name of the validated property or field path.</param>
/// <param name="ErrorMessage">The human-readable error message.</param>
/// <param name="ErrorCode">The optional machine-readable error code (e.g. "ERR_EMAIL_INVALID").</param>
/// <param name="Severity">The severity level (Error, Warning, Info) of this failure.</param>
/// <param name="RuleSet">The RuleSet under which the rule was executed.</param>
/// <param name="MessageKey">An optional localization lookup key.</param>
/// <param name="AttemptedValue">The property value that caused the validation to fail.</param>
/// <param name="Metadata">Arbitrary structured metadata key-value pairs associated with the failure.</param>
/// <param name="FieldPath">The mapped client-facing field path (e.g. "user.addresses[0].zip").</param>
/// <param name="Groups">The collection of logical group tags (e.g. ["UiHints", "Security"]) assigned to this failure.</param>
/// <example>
/// <code>
/// var failure = new KyrolusValidationFailure(
///     PropertyName: "Email",
///     ErrorMessage: "The email address is invalid.",
///     ErrorCode: "INVALID_EMAIL",
///     Severity: KyrolusValidationSeverity.Error,
///     Groups: ["UiHints", "Account"]);
/// </code>
/// </example>
public sealed record KyrolusValidationFailure(
    string PropertyName,
    string ErrorMessage,
    string? ErrorCode = null,
    KyrolusValidationSeverity Severity = KyrolusValidationSeverity.Error,
    string? RuleSet = null,
    string? MessageKey = null,
    object? AttemptedValue = null,
    IReadOnlyDictionary<string, object?>? Metadata = null,
    string? FieldPath = null,
    IReadOnlyList<string>? Groups = null);
