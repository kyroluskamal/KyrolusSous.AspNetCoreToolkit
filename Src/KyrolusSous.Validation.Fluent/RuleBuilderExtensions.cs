using System.Text.RegularExpressions;

namespace KyrolusSous.Validation.Fluent;

public static partial class RuleBuilderExtensions
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsNotEmpty(string? value)
        => !string.IsNullOrWhiteSpace(value);

    public static bool IsNotEmpty<T>(IEnumerable<T>? collection)
        => collection is not null && collection.Any();

    public static bool IsNotEmpty(Guid value)
        => value != Guid.Empty;

    public static bool IsNotNull<T>(T? value)
        => value is not null;

    public static bool IsLengthValid(string? value, int min, int max)
        => value is not null && value.Length >= min && value.Length <= max;

    public static bool IsExactLengthValid(string? value, int length)
        => value is not null && value.Length == length;

    public static bool IsGreaterThan<T>(T value, T limit) where T : IComparable<T>
        => value is not null && value.CompareTo(limit) > 0;

    public static bool IsGreaterThanOrEqualTo<T>(T value, T limit) where T : IComparable<T>
        => value is not null && value.CompareTo(limit) >= 0;

    public static bool IsLessThan<T>(T value, T limit) where T : IComparable<T>
        => value is not null && value.CompareTo(limit) < 0;

    public static bool IsLessThanOrEqualTo<T>(T value, T limit) where T : IComparable<T>
        => value is not null && value.CompareTo(limit) <= 0;

    public static bool IsInclusiveBetween<T>(T value, T from, T to) where T : IComparable<T>
        => value is not null && value.CompareTo(from) >= 0 && value.CompareTo(to) <= 0;

    public static bool IsEmailAddress(string? value)
        => !string.IsNullOrWhiteSpace(value) && EmailRegex.IsMatch(value);

    public static bool IsRegexMatch(string? value, string pattern)
        => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, pattern);

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
