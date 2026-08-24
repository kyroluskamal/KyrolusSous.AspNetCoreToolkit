using System.Text.RegularExpressions;

namespace KyrolusSous.Logging.Core.Redaction;

/// <summary>
/// High-performance, 100% Native AOT regex-based redactor for sensitive patterns (JWT, Bearer, Credit Cards, URLs, JSON).
/// </summary>
public sealed partial class KyrolusStringRedactor : IKyrolusStringRedactor
{
    private const string Mask = "***";

    /// <inheritdoc/>
    public string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        var result = input;

        // 1. Redact JWT tokens
        result = JwtRegex().Replace(result, Mask);

        // 2. Redact Bearer Authorization tokens
        result = BearerRegex().Replace(result, "$1" + Mask);

        // 3. Redact Sensitive JSON / Key-Value fields
        result = JsonSensitiveFieldRegex().Replace(result, "$1" + Mask + "$2");

        // 4. Redact Sensitive URL / Key-Value Query Strings
        result = UrlSensitiveParamRegex().Replace(result, "$1$2" + Mask);

        // 5. Redact Validated Credit Card Numbers (Luhn Algorithm)
        result = RedactCreditCards(result);

        return result;
    }

    private static string RedactCreditCards(string input)
    {
        return CreditCardCandidateRegex().Replace(input, match =>
        {
            var raw = match.Value;
            var digitsOnly = raw.Replace(" ", string.Empty).Replace("-", string.Empty);
            if (digitsOnly.Length is >= 13 and <= 19 && IsValidLuhn(digitsOnly))
            {
                return Mask;
            }
            return raw;
        });
    }

    private static bool IsValidLuhn(string number)
    {
        var sum = 0;
        var alternate = false;
        for (var i = number.Length - 1; i >= 0; i--)
        {
            var c = number[i];
            if (!char.IsDigit(c))
            {
                return false;
            }

            var n = c - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }
            sum += n;
            alternate = !alternate;
        }
        return sum > 0 && sum % 10 == 0;
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(Bearer\s+)[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(?i)(""?(?:password|pwd|secret|token|apiKey|api_key|client_secret|cvv|ssn)""?\s*:\s*"")[^""]*(""""?)", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex JsonSensitiveFieldRegex();

    [GeneratedRegex(@"(?i)([?&;]|^|\s)((?:token|access_token|secret|password|pwd|api_key|apikey|key|client_secret)\s*=\s*)([^&#\s;]+)", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex UrlSensitiveParamRegex();

    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    private static partial Regex CreditCardCandidateRegex();
}
