namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a CQRS command or request as auditable, triggering an immutable audit trail emission upon execution.
/// </summary>
public interface IKyrolusAuditableCommand
{
    /// <summary>
    /// Gets the human-readable or technical action name for this audit event (e.g. "CreateOrder", "RefundPayment").
    /// Defaults to the request type name if null or empty.
    /// </summary>
    string? AuditAction => null;

    /// <summary>
    /// Gets the business action code or localization key for this audit event (e.g. "Orders.Create", "Payroll.Disburse").
    /// Defaults to <see cref="AuditAction"/> if null.
    /// </summary>
    string? BusinessAction => AuditAction;

    /// <summary>
    /// Gets optional arguments used to format the localized audit message template.
    /// </summary>
    object? BusinessActionArgs => null;

    /// <summary>
    /// Gets the category or business module for this audit event (e.g. "Billing", "Identity").
    /// </summary>
    string? AuditCategory => null;

    /// <summary>
    /// Gets a value indicating whether the request payload should be recorded in the audit entry.
    /// Default is <c>true</c>.
    /// </summary>
    bool IncludePayload => true;
}
