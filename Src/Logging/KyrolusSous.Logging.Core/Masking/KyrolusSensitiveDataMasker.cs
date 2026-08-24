using KyrolusSous.Logging.Core.Redaction;

namespace KyrolusSous.Logging.Core.Masking;

/// <summary>
/// Default thread-safe implementation of <see cref="IKyrolusDataMasker"/> with smart keyword detection and inline pattern redaction.
/// </summary>
public sealed class KyrolusSensitiveDataMasker : IKyrolusDataMasker
{
    private static readonly string[] DefaultSensitiveKeywords =
    {
        "password",
        "pwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "accesstoken",
        "access_token",
        "refreshtoken",
        "refresh_token",
        "creditcard",
        "credit_card",
        "cardnumber",
        "card_number",
        "cvv",
        "cvc",
        "ssn",
        "socialsecurity",
        "social_security",
        "authorization",
        "auth",
        "bearer",
        "privatekey",
        "private_key",
        "clientsecret",
        "client_secret"
    };

    private readonly HashSet<string> _sensitiveKeywords;
    private readonly IKyrolusStringRedactor _stringRedactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusSensitiveDataMasker"/> class.
    /// </summary>
    public KyrolusSensitiveDataMasker(
        IEnumerable<string>? customSensitiveKeywords = null,
        IKyrolusStringRedactor? stringRedactor = null)
    {
        _stringRedactor = stringRedactor ?? new KyrolusStringRedactor();
        _sensitiveKeywords = new HashSet<string>(DefaultSensitiveKeywords, StringComparer.OrdinalIgnoreCase);
        if (customSensitiveKeywords is not null)
        {
            foreach (var kw in customSensitiveKeywords.Where(kw => !string.IsNullOrWhiteSpace(kw)))
            {
                _sensitiveKeywords.Add(kw.Trim());
            }
        }
    }

    /// <summary>
    /// Checks whether a property name contains or matches any known sensitive keyword with boundary awareness.
    /// </summary>
    public bool IsSensitivePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        if (_sensitiveKeywords.Contains(propertyName))
        {
            return true;
        }

        foreach (var keyword in _sensitiveKeywords)
        {
            if (IsKeywordMatch(propertyName, keyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKeywordMatch(string propertyName, string keyword)
    {
        var index = propertyName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        // Long and specific keywords (>= 6 chars like "password", "creditcard", "clientsecret") are safe with direct substring match
        if (keyword.Length >= 6)
        {
            return true;
        }

        // For short keywords ("key", "pwd", "auth", "cvv", "ssn"), check word boundaries to avoid false positives like "Author" or "Keyboard"
        var beforeIndex = index - 1;
        var afterIndex = index + keyword.Length;

        var validStart = beforeIndex < 0 || !char.IsLetter(propertyName[beforeIndex]) || (char.IsLower(propertyName[beforeIndex]) && char.IsUpper(propertyName[index]));
        var validEnd = afterIndex >= propertyName.Length || !char.IsLetter(propertyName[afterIndex]) || char.IsUpper(propertyName[afterIndex]);

        return validStart && validEnd;
    }

    /// <summary>
    /// Masks a string value according to specified or default masking rules.
    /// </summary>
    public string MaskString(string? value, KyrolusMaskedAttribute? rule = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (rule is null)
        {
            return "***MASKED***";
        }

        var showFirst = Math.Max(0, rule.ShowFirst);
        var showLast = Math.Max(0, rule.ShowLast);

        if (showFirst + showLast >= value.Length)
        {
            return new string(rule.MaskCharacter, value.Length);
        }

        var prefix = showFirst > 0 ? value.Substring(0, showFirst) : string.Empty;
        var suffix = showLast > 0 ? value.Substring(value.Length - showLast) : string.Empty;
        var maskLen = rule.PreserveLength ? value.Length - showFirst - showLast : 4;
        var mask = new string(rule.MaskCharacter, Math.Max(1, maskLen));

        return $"{prefix}{mask}{suffix}";
    }

    /// <summary>
    /// Sanitizes a dictionary of structured log properties, replacing sensitive values with masked equivalents
    /// and redacting inline sensitive patterns from strings.
    /// </summary>
    public IReadOnlyDictionary<string, object?> SanitizeProperties(IReadOnlyDictionary<string, object?>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return properties ?? new Dictionary<string, object?>();
        }

        var result = new Dictionary<string, object?>(properties.Count, StringComparer.Ordinal);
        foreach (var (key, val) in properties)
        {
            if (IsSensitivePropertyName(key))
            {
                result[key] = val is string s ? MaskString(s) : "***MASKED***";
            }
            else if (val is string strVal)
            {
                result[key] = _stringRedactor.Redact(strVal);
            }
            else
            {
                result[key] = val;
            }
        }

        return result;
    }
}
