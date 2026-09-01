namespace KyrolusSous.Validation.Fluent;

/// <summary>
/// Password complexity requirements checked by <see cref="AdvancedRuleBuilderExtensions.IsStrongPasswordValid"/>
/// and the Fluent DSL's <c>.StrongPassword()</c>. All requirements default to on; construct with named
/// parameters to relax the ones that don't apply.
/// </summary>
/// <param name="MinLength">Minimum allowed password length.</param>
/// <param name="MaxLength">Maximum allowed password length.</param>
/// <param name="RequireUppercase">Whether at least one uppercase letter is required.</param>
/// <param name="RequireLowercase">Whether at least one lowercase letter is required.</param>
/// <param name="RequireDigit">Whether at least one digit is required.</param>
/// <param name="RequireSpecialChar">Whether at least one non-alphanumeric character is required.</param>
/// <example>
/// <code>
/// RuleFor(x => x.Password).StrongPassword(new KyrolusPasswordOptions(MinLength: 12, RequireSpecialChar: false));
/// </code>
/// </example>
public sealed record KyrolusPasswordOptions(
    int MinLength = 8,
    int MaxLength = 128,
    bool RequireUppercase = true,
    bool RequireLowercase = true,
    bool RequireDigit = true,
    bool RequireSpecialChar = true);

/// <summary>
/// Domain-specific format checks (national IDs, IBAN, passwords, JSON/Base64, Cron, MAC addresses) that back the
/// matching Fluent DSL extensions in <see cref="RuleBuilderFluentExtensions"/> (e.g. <c>.NationalId()</c>,
/// <c>.IbanValid()</c>). Like <see cref="RuleBuilderExtensions"/>, every check here is also callable directly
/// outside the Fluent DSL - useful in a hand-written validator or a unit test.
/// </summary>
public static class AdvancedRuleBuilderExtensions
{
    /// <summary>Matches a MAC address in colon/hyphen-separated (<c>AA:BB:CC:DD:EE:FF</c>) or bare 12-hex-digit form. Carries a match timeout to guard against ReDoS.</summary>
    private static readonly Regex MacRegex = new(
        @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$|^([0-9A-Fa-f]{12})$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    /// <summary>The 23-letter control-letter alphabet used by both Spanish DNI and NIE checksums (indexed by <c>number mod 23</c>).</summary>
    private const string SpanishDniControlLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

    /// <summary>The 10-letter control-letter alphabet used by the Spanish CIF checksum (indexed by the computed control digit).</summary>
    private const string SpanishCifControlLetters = "JABCDEFGHI";

    /// <summary>
    /// Validates National ID numbers. Default country "EG" performs full 14-digit Egyptian National ID algorithm
    /// including century, valid birth date YYMMDD, governorate code, and checksum.
    /// Supports "ES", "ES-DNI", "ES-NIE", "ES-CIF" for Spanish identification numbers.
    /// </summary>
    /// <param name="nationalId">The national ID string to validate.</param>
    /// <param name="countryCode">
    /// Which country's rules to apply (case-insensitive). "EG"/"EGYPT" for Egypt (default); "ES"/"ES-NIF"/"SPAIN"
    /// for any Spanish NIF (DNI, NIE, or CIF); "ES-DNI", "ES-NIE", "ES-CIF" to require one specific Spanish
    /// document type. Any other value falls back to a generic length/alphanumeric check.
    /// </param>
    /// <example>
    /// <code>
    /// AdvancedRuleBuilderExtensions.IsNationalIdValid("29812250101231");             // Egyptian, default
    /// AdvancedRuleBuilderExtensions.IsNationalIdValid("12345678Z", "ES-DNI");         // Spanish DNI specifically
    /// RuleFor(x => x.NationalId).NationalId(countryCode: "ES");                      // via the Fluent DSL
    /// </code>
    /// </example>
    public static bool IsNationalIdValid(string? nationalId, string countryCode = "EG")
    {
        if (string.IsNullOrWhiteSpace(nationalId)) return false;
        var sanitized = nationalId.Trim();

        return countryCode.ToUpperInvariant() switch
        {
            "EG" or "EGYPT" => IsEgyptianNationalIdValid(sanitized),
            "ES" or "ES-NIF" or "SPAIN" => IsSpanishNifValid(sanitized),
            "ES-DNI" => IsSpanishDniValid(sanitized),
            "ES-NIE" => IsSpanishNieValid(sanitized),
            "ES-CIF" => IsSpanishCifValid(sanitized),
            _ => sanitized.Length >= 6 && sanitized.All(char.IsLetterOrDigit)
        };
    }

    /// <summary>
    /// The Egyptian 14-digit National ID algorithm: century digit (2 or 3) + 2-digit year, 2-digit month,
    /// 2-digit day forming a real calendar date, a governorate code in 01-88, then a Modulo-11 checksum digit
    /// (weights 2,7,6,5,4,3,2,7,6,5,4,3,2 applied to the first 13 digits) matching the 14th digit.
    /// </summary>
    private static bool IsEgyptianNationalIdValid(string sanitized)
    {
        if (sanitized.Length != 14 || !sanitized.All(char.IsDigit)) return false;

        // Century check (2 = 1900-1999, 3 = 2000-2099)
        int centuryDigit = sanitized[0] - '0';
        if (centuryDigit != 2 && centuryDigit != 3) return false;

        int year = (centuryDigit == 2 ? 1900 : 2000) + int.Parse(sanitized.Substring(1, 2));
        int month = int.Parse(sanitized.Substring(3, 2));
        int day = int.Parse(sanitized.Substring(5, 2));

        if (!DateOnly.TryParseExact($"{year:D4}-{month:D2}-{day:D2}", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)) return false;

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

    /// <summary>
    /// Validates Spanish DNI (Documento Nacional de Identidad) for Spanish individuals (8 digits + 1 control letter).
    /// </summary>
    /// <param name="dni">The DNI string to validate (spaces/hyphens are stripped before checking).</param>
    public static bool IsSpanishDniValid(string? dni)
    {
        if (string.IsNullOrWhiteSpace(dni)) return false;
        var sanitized = dni.Replace(" ", "").Replace("-", "").ToUpperInvariant();

        if (sanitized.Length != 9) return false;

        var numberPart = sanitized.Substring(0, 8);
        if (!numberPart.All(char.IsDigit)) return false;

        int number = int.Parse(numberPart);
        char expectedLetter = SpanishDniControlLetters[number % 23];

        return sanitized[8] == expectedLetter;
    }

    /// <summary>
    /// Validates Spanish NIE (Número de Identidad de Extranjero) for resident foreigners (X, Y, or Z + 7 digits + 1 control letter).
    /// </summary>
    /// <param name="nie">The NIE string to validate (spaces/hyphens are stripped before checking).</param>
    public static bool IsSpanishNieValid(string? nie)
    {
        if (string.IsNullOrWhiteSpace(nie)) return false;
        var sanitized = nie.Replace(" ", "").Replace("-", "").ToUpperInvariant();

        if (sanitized.Length != 9) return false;

        char firstChar = sanitized[0];
        char prefixDigit = firstChar switch
        {
            'X' => '0',
            'Y' => '1',
            'Z' => '2',
            _ => '\0'
        };

        if (prefixDigit == '\0') return false;

        var middleDigits = sanitized.Substring(1, 7);
        if (!middleDigits.All(char.IsDigit)) return false;

        int number = int.Parse($"{prefixDigit}{middleDigits}");
        char expectedLetter = SpanishDniControlLetters[number % 23];

        return sanitized[8] == expectedLetter;
    }

    /// <summary>
    /// Validates Spanish CIF (Código de Identificación Fiscal) for legal entities and corporations (1 letter + 7 digits + 1 control digit/letter).
    /// </summary>
    /// <param name="cif">The CIF string to validate (spaces/hyphens are stripped before checking).</param>
    public static bool IsSpanishCifValid(string? cif)
    {
        if (string.IsNullOrWhiteSpace(cif)) return false;
        var sanitized = cif.Replace(" ", "").Replace("-", "").ToUpperInvariant();

        if (sanitized.Length != 9) return false;

        char prefix = sanitized[0];
        const string validPrefixes = "ABCDEFGHJKLMNPQRSUVW";
        if (!validPrefixes.Contains(prefix)) return false;

        var digitsPart = sanitized.Substring(1, 7);
        if (!digitsPart.All(char.IsDigit)) return false;

        int sumEven = 0;
        int sumOdd = 0;

        for (int i = 0; i < 7; i++)
        {
            int digit = digitsPart[i] - '0';
            if ((i + 1) % 2 == 0) // 2nd, 4th, 6th digit (1-based)
            {
                sumEven += digit;
            }
            else // 1st, 3rd, 5th, 7th digit (1-based)
            {
                int multiplied = digit * 2;
                sumOdd += (multiplied / 10) + (multiplied % 10);
            }
        }

        int totalSum = sumEven + sumOdd;
        int controlDigit = (10 - (totalSum % 10)) % 10;
        char controlLetter = SpanishCifControlLetters[controlDigit];

        char lastChar = sanitized[8];

        // Organizations requiring letter control
        const string letterOnlyPrefixes = "PQSKWNRV";
        // Organizations requiring number control
        const string numberOnlyPrefixes = "ABEH";

        if (letterOnlyPrefixes.Contains(prefix))
        {
            return lastChar == controlLetter;
        }

        if (numberOnlyPrefixes.Contains(prefix))
        {
            return lastChar == (char)('0' + controlDigit);
        }

        // Remaining prefixes (C, D, F, G, J, L, M, U) accept either digit or letter
        return lastChar == (char)('0' + controlDigit) || lastChar == controlLetter;
    }

    /// <summary>
    /// Validates Spanish NIF (Número de Identificación Fiscal) which encompasses DNI (individuals), NIE (foreigners), and CIF (companies).
    /// </summary>
    /// <param name="nif">The document string to validate against all three Spanish document formats.</param>
    public static bool IsSpanishNifValid(string? nif)
    {
        if (string.IsNullOrWhiteSpace(nif)) return false;
        return IsSpanishDniValid(nif) || IsSpanishNieValid(nif) || IsSpanishCifValid(nif);
    }

    /// <summary>
    /// Validates International Bank Account Number (IBAN) using ISO 7064 Modulo 97.
    /// </summary>
    /// <param name="iban">The IBAN string to validate (spaces/hyphens are stripped before checking).</param>
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
    /// <param name="password">The password string to validate.</param>
    /// <param name="options">The complexity requirements to enforce; defaults to a new <see cref="KyrolusPasswordOptions"/> (all requirements on) when omitted.</param>
    public static bool IsStrongPasswordValid(string? password, KyrolusPasswordOptions? options = null)
    {
        if (string.IsNullOrEmpty(password)) return false;
        options ??= new KyrolusPasswordOptions();

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
    /// <param name="json">The JSON text to check for well-formedness (syntax only - this does not validate against a schema).</param>
    public static bool IsJsonValid(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);

        try
        {
            while (reader.Read())
            {
                _ = reader.TokenType;
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates Base64 encoded strings safely without risking StackOverflowException on large inputs.
    /// </summary>
    /// <param name="base64">The string to check. Whitespace is trimmed first; internal whitespace is not permitted.</param>
    public static bool IsBase64Valid(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return false;
        var trimmed = base64.Trim();
        if (trimmed.Length % 4 != 0) return false;

        const int stackAllocThreshold = 256;
        if (trimmed.Length <= stackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[stackAllocThreshold];
            return Convert.TryFromBase64String(trimmed, buffer, out _);
        }

        byte[] rented = System.Buffers.ArrayPool<byte>.Shared.Rent(trimmed.Length);
        try
        {
            return Convert.TryFromBase64String(trimmed, rented, out _);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Validates Geographical Coordinates (Latitude between -90 and 90, Longitude between -180 and 180).
    /// </summary>
    /// <param name="latitude">The latitude value, in degrees.</param>
    /// <param name="longitude">The longitude value, in degrees.</param>
    public static bool IsCoordinatesValid(double latitude, double longitude)
        => latitude >= -90.0 && latitude <= 90.0 && longitude >= -180.0 && longitude <= 180.0;

    /// <summary>The valid (Min, Max) numeric range for each of the 5 standard cron fields, in order: minute, hour, day-of-month, month, day-of-week.</summary>
    private static readonly (int Min, int Max)[] CronFieldRanges =
    [
        (0, 59), // minute
        (0, 23), // hour
        (1, 31), // day of month
        (1, 12), // month
        (0, 7)   // day of week (0 and 7 both mean Sunday)
    ];

    /// <summary>
    /// Validates 5-part standard cron expressions, including that numeric values/ranges/steps fall within each
    /// field's real bounds (e.g. minutes 0-59) - not just that the characters used look cron-shaped.
    /// </summary>
    /// <param name="cron">The 5-field cron expression (minute hour day-of-month month day-of-week) to validate.</param>
    public static bool IsCronExpressionValid(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return false;
        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;

        for (var i = 0; i < parts.Length; i++)
            if (!IsCronFieldValid(parts[i], CronFieldRanges[i].Min, CronFieldRanges[i].Max))
                return false;

        return true;
    }

    /// <summary>
    /// Validates one cron field against <paramref name="min"/>/<paramref name="max"/>, supporting comma-separated
    /// lists, <c>*</c>, ranges (<c>a-b</c>), and step syntax (<c>.../n</c> on a <c>*</c> or a range) - checking that
    /// every numeric value named actually falls within bounds, not just that the syntax looks well-formed.
    /// </summary>
    private static bool IsCronFieldValid(string field, int min, int max)
    {
        foreach (var listItem in field.Split(','))
        {
            if (listItem.Length == 0) return false;

            var slashIndex = listItem.IndexOf('/');
            var basePart = slashIndex >= 0 ? listItem[..slashIndex] : listItem;
            var stepPart = slashIndex >= 0 ? listItem[(slashIndex + 1)..] : null;

            if (stepPart is not null && (!int.TryParse(stepPart, out var step) || step <= 0))
                return false;

            if (basePart == "*")
                continue;

            var dashIndex = basePart.IndexOf('-');
            if (dashIndex > 0)
            {
                if (!int.TryParse(basePart[..dashIndex], out var start) || !int.TryParse(basePart[(dashIndex + 1)..], out var end))
                    return false;
                if (start < min || start > max || end < min || end > max || start > end)
                    return false;
            }
            else
            {
                if (!int.TryParse(basePart, out var value) || value < min || value > max)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates MAC Address formats.
    /// </summary>
    /// <param name="mac">The MAC address string, in colon/hyphen-separated (<c>AA:BB:CC:DD:EE:FF</c>) or bare 12-hex-digit form.</param>
    public static bool IsMacAddressValid(string? mac)
        => !string.IsNullOrWhiteSpace(mac) && MacRegex.IsMatch(mac.Trim());
}
