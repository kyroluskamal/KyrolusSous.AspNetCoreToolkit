namespace KyrolusSous.Auth.Security;

/// <summary>
/// Configuration options for password complexity and security policies.
/// </summary>
public sealed class KyrolusPasswordPolicyOptions
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 128;
    public bool RequireDigit { get; set; } = true;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireNonAlphanumeric { get; set; } = true;
    public int RequiredUniqueChars { get; set; } = 4;
    public HashSet<string> ForbiddenPasswords { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password123", "admin", "admin123", "12345678", "qwertyuiop", "letmein"
    };
}

/// <summary>
/// Represents the evaluation outcome of a password policy check.
/// </summary>
public sealed class KyrolusPasswordPolicyResult
{
    public bool Succeeded => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; private init; } = [];
    public int Score { get; private init; } // 0 to 4 strength score

    public static KyrolusPasswordPolicyResult Success(int score)
        => new() { Errors = [], Score = score };

    public static KyrolusPasswordPolicyResult Failed(IReadOnlyList<string> errors, int score = 0)
        => new() { Errors = errors, Score = score };
}
