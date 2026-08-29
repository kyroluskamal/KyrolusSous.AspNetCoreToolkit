using System.Text.RegularExpressions;

namespace KyrolusSous.Payments.Abstractions;

public static partial class KyrolusPaymentDataMasker
{
    private static readonly Regex CardRegex = new(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.Compiled);
    private static readonly Regex CvvRegex = new(@"(?i)(""(?:cvv|cvc|securityCode|security_code)""\s*:\s*"")([^""]+)("")", RegexOptions.Compiled);
    private static readonly Regex SecretRegex = new(@"(?i)(""(?:apiKey|secretKey|clientSecret|api_key|secret_key|client_secret|password)""\s*:\s*"")([^""]+)("")", RegexOptions.Compiled);

    public static string MaskCardNumber(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return string.Empty;

        var digits = Regex.Replace(cardNumber, @"\D", "");
        if (digits.Length < 10) return "****";

        var first6 = digits[..6];
        var last4 = digits[^4..];
        var masked = new string('*', digits.Length - 10);

        return $"{first6}{masked}{last4}";
    }

    public static string RedactSensitivePayload(string? rawJsonOrText)
    {
        if (string.IsNullOrWhiteSpace(rawJsonOrText)) return string.Empty;

        var redacted = CvvRegex.Replace(rawJsonOrText, "$1***$3");
        redacted = SecretRegex.Replace(redacted, "$1[REDACTED]$3");

        redacted = CardRegex.Replace(redacted, match =>
        {
            var clean = Regex.Replace(match.Value, @"\D", "");
            return clean.Length is >= 13 and <= 19 ? MaskCardNumber(clean) : match.Value;
        });

        return redacted;
    }
}
