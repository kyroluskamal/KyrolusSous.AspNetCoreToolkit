namespace KyrolusSous.Validation.Fluent;

/// <summary>
/// The raw <c>bool</c> predicate behind each general-purpose check in <see cref="RuleBuilderFluentExtensions"/>
/// (e.g. <see cref="RuleBuilderFluentExtensions.NotEmpty{T}"/> calls <see cref="IsNotEmpty(string?)"/>). Exposed as
/// public static methods so the same logic is reusable outside the Fluent DSL too - a hand-written
/// <see cref="IKyrolusRequestValidator{TRequest}"/>, a unit test, or another rule-writing package can call
/// <c>RuleBuilderExtensions.IsEmailAddress(value)</c> directly without going through <c>RuleFor(...)</c> at all.
/// </summary>
/// <example>
/// <code>
/// if (!RuleBuilderExtensions.IsCreditCardValid(request.CardNumber))
///     failures.Add(new KyrolusValidationFailure(nameof(request.CardNumber), "Invalid card number."));
/// </code>
/// </example>
public static partial class RuleBuilderExtensions
{
    /// <summary>Default timeout applied to every regex match performed by this class, guarding against
    /// catastrophic backtracking on attacker-controlled input (ReDoS).</summary>
    private static readonly TimeSpan DefaultMatchTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        DefaultMatchTimeout);

    /// <summary>True when <paramref name="value"/> is not null, empty, or whitespace-only.</summary>
    public static bool IsNotEmpty(string? value)
        => !string.IsNullOrWhiteSpace(value);

    /// <summary>True when <paramref name="collection"/> is not null and contains at least one element.</summary>
    public static bool IsNotEmpty<T>(IEnumerable<T>? collection)
        => collection is not null && collection.Any();

    /// <summary>True when <paramref name="value"/> is not <see cref="Guid.Empty"/>.</summary>
    public static bool IsNotEmpty(Guid value)
        => value != Guid.Empty;

    /// <summary>True when <paramref name="value"/> is not <see langword="null"/>.</summary>
    public static bool IsNotNull<T>(T? value)
        => value is not null;

    /// <summary>True when <paramref name="value"/>'s length is within <paramref name="min"/>..<paramref name="max"/> inclusive.</summary>
    public static bool IsLengthValid(string? value, int min, int max)
        => value is not null && value.Length >= min && value.Length <= max;

    /// <summary>True when <paramref name="value"/>'s length is at least <paramref name="min"/>.</summary>
    public static bool IsMinLengthValid(string? value, int min)
        => value is not null && value.Length >= min;

    /// <summary>True when <paramref name="value"/>'s length does not exceed <paramref name="max"/>.</summary>
    public static bool IsMaxLengthValid(string? value, int max)
        => value is not null && value.Length <= max;

    /// <summary>True when <paramref name="value"/>'s length exactly equals <paramref name="length"/>.</summary>
    public static bool IsExactLengthValid(string? value, int length)
        => value is not null && value.Length == length;

    /// <summary>True when <paramref name="value"/> is a defined member of <typeparamref name="TEnum"/> (rejects out-of-range values cast into the enum's underlying type).</summary>
    public static bool IsInEnumValid<TEnum>(TEnum value) where TEnum : struct, Enum
        => Enum.IsDefined(value);

    /// <summary>True when <paramref name="value"/> has no more than <paramref name="scale"/> digits after the decimal point and no more than <paramref name="precision"/> digits in total.</summary>
    public static bool IsScalePrecisionValid(decimal value, int precision, int scale)
    {
        var bits = decimal.GetBits(value);
        byte actualScale = (byte)((bits[3] >> 16) & 0x7F);
        if (actualScale > scale) return false;

        // Calculate precision (number of total digits)
        string str = Math.Abs(value).ToString("G", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
        return str.Length <= precision;
    }

    /// <summary>True when <paramref name="value"/> is not null and strictly greater than <paramref name="limit"/>.</summary>
    public static bool IsGreaterThan<T>(T value, T limit) where T : IComparable<T>
        => value is not null && value.CompareTo(limit) > 0;

    /// <summary>True when <paramref name="value"/> is not null and greater than or equal to <paramref name="limit"/>.</summary>
    public static bool IsGreaterThanOrEqualTo<T>(T value, T limit) where T : IComparable<T>
        => value is not null && value.CompareTo(limit) >= 0;

    /// <summary>True when <paramref name="value"/> is not null and strictly less than <paramref name="limit"/>.</summary>
    public static bool IsLessThan<T>(T value, T limit) where T : IComparable<T>
        => value is not null && value.CompareTo(limit) < 0;

    /// <summary>True when <paramref name="value"/> is not null and less than or equal to <paramref name="limit"/>.</summary>
    public static bool IsLessThanOrEqualTo<T>(T value, T limit) where T : IComparable<T>
        => value is not null && value.CompareTo(limit) <= 0;

    /// <summary>True when <paramref name="value"/> equals <paramref name="expected"/> per <see cref="EqualityComparer{T}.Default"/>.</summary>
    public static bool IsEqual<T>(T value, T expected)
        => EqualityComparer<T>.Default.Equals(value, expected);

    /// <summary>True when <paramref name="value"/> does not equal <paramref name="expected"/> per <see cref="EqualityComparer{T}.Default"/>.</summary>
    public static bool IsNotEqual<T>(T value, T expected)
        => !EqualityComparer<T>.Default.Equals(value, expected);

    /// <summary>True when <paramref name="value"/> is null, empty, or whitespace-only (the inverse of <see cref="IsNotEmpty(string?)"/>).</summary>
    public static bool IsEmpty(string? value)
        => string.IsNullOrWhiteSpace(value);

    /// <summary>True when <paramref name="collection"/> is null or contains no elements.</summary>
    public static bool IsEmpty<T>(IEnumerable<T>? collection)
        => collection is null || !collection.Any();

    /// <summary>True when <paramref name="value"/> is <see langword="null"/>.</summary>
    public static bool IsNull<T>(T? value)
        => value is null;

    /// <summary>True when <paramref name="value"/> is strictly between <paramref name="from"/> and <paramref name="to"/> (both endpoints excluded).</summary>
    public static bool IsExclusiveBetween<T>(T value, T from, T to) where T : IComparable<T>
        => value is not null && value.CompareTo(from) > 0 && value.CompareTo(to) < 0;

    /// <summary>True when <paramref name="value"/> is between <paramref name="from"/> and <paramref name="to"/> (both endpoints included).</summary>
    public static bool IsInclusiveBetween<T>(T value, T from, T to) where T : IComparable<T>
        => value is not null && value.CompareTo(from) >= 0 && value.CompareTo(to) <= 0;

    /// <summary>
    /// True when <paramref name="value"/> matches a simple <c>local@domain.tld</c> shape. This is a permissive
    /// format check, not full RFC 5322 validation - it's intentionally lenient about what a "local" or "domain"
    /// part may contain, catching only the clearly-malformed cases (missing @, missing dot, embedded whitespace).
    /// </summary>
    public static bool IsEmailAddress(string? value)
        => !string.IsNullOrWhiteSpace(value) && EmailRegex.IsMatch(value);

    /// <summary>
    /// True when <paramref name="value"/> matches <paramref name="pattern"/>. A match that runs longer than
    /// <paramref name="matchTimeout"/> (default <see cref="DefaultMatchTimeout"/>, 250ms) is treated as "did not
    /// match" rather than throwing, so a pathological pattern/input pair can't hang the calling request.
    /// </summary>
    public static bool IsRegexMatch(string? value, string pattern, TimeSpan? matchTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.None, matchTimeout ?? DefaultMatchTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            // Treat a runaway match (catastrophic backtracking / ReDoS attempt) as "did not match" rather than
            // letting the exception propagate out of a validation rule.
            return false;
        }
    }

    /// <summary>
    /// True when <paramref name="value"/> (after stripping spaces and hyphens) is a 13-19 digit number that
    /// passes the Luhn checksum - the standard structural check shared by all major card networks. This confirms
    /// the number is <em>well-formed</em>, not that it belongs to a real, active, chargeable card.
    /// </summary>
    public static bool IsCreditCardValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var sanitized = value.Replace("-", "").Replace(" ", "");
        if (!sanitized.All(char.IsDigit) || sanitized.Length < 13 || sanitized.Length > 19) return false;

        // Luhn Algorithm Check
        int sum = 0;
        bool isSecond = false;
        for (int i = sanitized.Length - 1; i >= 0; i--)
        {
            int d = sanitized[i] - '0';
            if (isSecond)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            isSecond = !isSecond;
        }

        return sum % 10 == 0;
    }
}
