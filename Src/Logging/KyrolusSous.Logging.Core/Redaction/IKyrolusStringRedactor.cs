namespace KyrolusSous.Logging.Core.Redaction;

/// <summary>
/// Contract for scanning and redacting sensitive text patterns (credit cards, tokens, credentials) in raw strings.
/// </summary>
public interface IKyrolusStringRedactor
{
    /// <summary>
    /// Redacts known sensitive patterns (JWTs, Credit Cards, Bearer tokens, URL query parameters) from the input string.
    /// </summary>
    /// <param name="input">The raw string to scan and redact.</param>
    /// <returns>The sanitized string with sensitive portions replaced by a mask.</returns>
    string Redact(string? input);
}
