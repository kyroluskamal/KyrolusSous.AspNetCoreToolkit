namespace KyrolusSous.CQRS.Abstractions.Audit;

/// <summary>
/// Configures which property and dictionary-key names <see cref="Behaviors.KyrolusAuditBehavior{TRequest,TResponse}"/>
/// treats as sensitive and redacts before handing a payload to the audit sink.
/// </summary>
/// <remarks>
/// The library ships a built-in keyword list (password, secret, token, pin, cvv, cardnumber, apikey)
/// that cannot anticipate every application's own field names. Register these via
/// <c>AddKyrolusCqrsAudit</c>'s <c>configureSanitization</c> delegate; to source them from
/// <c>appsettings.json</c> instead of hard-coding them, bind the delegate's options to a configuration
/// section:
/// <code>
/// services.AddKyrolusCqrsAudit(configureSanitization: opts =>
///     configuration.GetSection("Kyrolus:Cqrs:Audit:Sanitization").Bind(opts));
/// </code>
/// with, for example:
/// <code>
/// { "Kyrolus": { "Cqrs": { "Audit": { "Sanitization": { "AdditionalSensitiveKeywords": ["ApiKey", "NationalId"] } } } } }
/// </code>
/// </remarks>
public sealed class KyrolusAuditSanitizationOptions
{
    /// <summary>
    /// Additional substrings (case-insensitive) checked against a property or dictionary-key name, on
    /// top of the library's built-in list.
    /// </summary>
    public IReadOnlyList<string> AdditionalSensitiveKeywords { get; set; } = [];
}
