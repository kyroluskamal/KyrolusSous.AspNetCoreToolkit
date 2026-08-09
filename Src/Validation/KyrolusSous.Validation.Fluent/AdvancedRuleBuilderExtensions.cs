using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KyrolusSous.Validation.Fluent;

public sealed record PasswordOptions(
    int MinLength = 8,
    int MaxLength = 128,
    bool RequireUppercase = true,
    bool RequireLowercase = true,
    bool RequireDigit = true,
    bool RequireSpecialChar = true);

public static class AdvancedRuleBuilderExtensions
{
    private static readonly Regex MacRegex = new(
        @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$|^([0-9A-Fa-f]{12})$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates National ID numbers. Default country "EG" performs full 14-digit Egyptian National ID algorithm
    /// including century, valid birth date YYMMDD, governorate code, and checksum.
    /// </summary>
    public static bool IsNationalIdValid(string? nationalId, string countryCode = "EG")
    {
        if (string.IsNullOrWhiteSpace(nationalId)) return false;
        var sanitized = nationalId.Trim();

        if (string.Equals(countryCode, "EG", StringComparison.OrdinalIgnoreCase))
        {
            if (sanitized.Length != 14 || !sanitized.All(char.IsDigit)) return false;

            // Century check (2 = 1900-1999, 3 = 2000-2099)
            int centuryDigit = sanitized[0] - '0';
            if (centuryDigit != 2 && centuryDigit != 3) return false;

            int year = (centuryDigit == 2 ? 1900 : 2000) + int.Parse(sanitized.Substring(1, 2));
            int month = int.Parse(sanitized.Substring(3, 2));
            int day = int.Parse(sanitized.Substring(5, 2));

            if (!DateTime.TryParse($"{year:D4}-{month:D2}-{day:D2}", out _)) return false;

            // Governorate code check (01 to 88)
            int govCode = int.Parse(sanitized.Substring(7, 2));
            if (govCode < 1 || govCode > 88) return false;

            // Egyptian Checksum (Modulo 11 with weights 2,7,6,5,4,3,2,7,6,5,4,3,2)
            int[] weights = [2, 7, 6, 5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
            int sum = 0;
            for (int i = 0; i < 13; i++)
            {
                sum += (sanitized[i] - '0') * weights[i];
            }

            int checkDigit = 11 - (sum % 11);
            if (checkDigit == 11) checkDigit = 0;
            if (checkDigit == 10) checkDigit = 1;

            int lastDigit = sanitized[13] - '0';
            return checkDigit == lastDigit;
        }

        return sanitized.Length >= 6 && sanitized.All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Validates International Bank Account Number (IBAN) using ISO 7064 Modulo 97.
    /// </summary>
    public static bool IsIbanValid(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return false;
        var sanitized = iban.Replace(" ", "").Replace("-", "").ToUpperInvariant();

        if (sanitized.Length < 15 || sanitized.Length > 34) return false;
        if (!char.IsLetter(sanitized[0]) || !char.IsLetter(sanitized[1])) return false;

        // Move first 4 chars (country + check digits) to end
        var rearranged = sanitized.Substring(4) + sanitized.Substring(0, 4);

        // Convert letters to numbers (A=10 .. Z=35)
        var numericSb = new System.Text.StringBuilder();
        foreach (char ch in rearranged)
        {
            if (char.IsDigit(ch))
            {
                numericSb.Append(ch);
            }
            else if (char.IsLetter(ch))
            {
                numericSb.Append(ch - 'A' + 10);
            }
            else
            {
                return false;
            }
        }

        if (!BigInteger.TryParse(numericSb.ToString(), out var bigInt)) return false;
        return bigInt % 97 == 1;
    }

    /// <summary>
    /// Validates Password Strength (Min/Max length, Uppercase, Lowercase, Digit, Special Characters).
    /// </summary>
    public static bool IsStrongPasswordValid(string? password, PasswordOptions? options = null)
    {
        if (string.IsNullOrEmpty(password)) return false;
        options ??= new PasswordOptions();

        if (password.Length < options.MinLength || password.Length > options.MaxLength) return false;
        if (options.RequireUppercase && !password.Any(char.IsUpper)) return false;
        if (options.RequireLowercase && !password.Any(char.IsLower)) return false;
        if (options.RequireDigit && !password.Any(char.IsDigit)) return false;
        if (options.RequireSpecialChar && !password.Any(ch => !char.IsLetterOrDigit(ch))) return false;

        return true;
    }

    /// <summary>
    /// Validates structural JSON strings without allocations using Utf8JsonReader.
    /// </summary>
    public static bool IsJsonValid(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);

        try
        {
            while (reader.Read()) { }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates Base64 encoded strings.
    /// </summary>
    public static bool IsBase64Valid(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return false;
        Span<byte> buffer = stackalloc byte[base64.Length];
        return Convert.TryFromBase64String(base64, buffer, out _);
    }

    /// <summary>
    /// Validates Geographical Coordinates (Latitude between -90 and 90, Longitude between -180 and 180).
    /// </summary>
    public static bool IsCoordinatesValid(double latitude, double longitude)
        => latitude >= -90.0 && latitude <= 90.0 && longitude >= -180.0 && longitude <= 180.0;

    /// <summary>
    /// Validates 5-part standard cron expressions.
    /// </summary>
    public static bool IsCronExpressionValid(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return false;
        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;

        foreach (var part in parts)
        {
            if (part != "*" && !part.All(ch => char.IsDigit(ch) || ch == '/' || ch == '-' || ch == ','))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates MAC Address formats.
    /// </summary>
    public static bool IsMacAddressValid(string? mac)
        => !string.IsNullOrWhiteSpace(mac) && MacRegex.IsMatch(mac.Trim());
}
