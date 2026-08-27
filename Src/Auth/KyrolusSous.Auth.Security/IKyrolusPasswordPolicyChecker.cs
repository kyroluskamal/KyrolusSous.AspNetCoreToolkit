namespace KyrolusSous.Auth.Security;

/// <summary>
/// Service contract for validating password strength and compliance with security policies.
/// </summary>
public interface IKyrolusPasswordPolicyChecker
{
    KyrolusPasswordPolicyResult Check(string password, KyrolusPasswordPolicyOptions? customOptions = null);

    /// <summary>
    /// Checks whether the candidate password matches any previously used password hash (NIST SP 800-63B reuse rule).
    /// </summary>
    bool IsPasswordPreviouslyUsed(string newPassword, IEnumerable<string> previousPasswordHashes, Func<string, string, bool> verifyPasswordHash);
}

/// <summary>
/// Default high-performance, AOT-friendly password policy checker implementation.
/// </summary>
public sealed class KyrolusPasswordPolicyChecker : IKyrolusPasswordPolicyChecker
{
    private readonly KyrolusPasswordPolicyOptions _defaultOptions;

    public KyrolusPasswordPolicyChecker(KyrolusPasswordPolicyOptions? defaultOptions = null)
    {
        _defaultOptions = defaultOptions ?? new KyrolusPasswordPolicyOptions();
    }

    public KyrolusPasswordPolicyResult Check(string password, KyrolusPasswordPolicyOptions? customOptions = null)
    {
        var options = customOptions ?? _defaultOptions;
        if (options.MinLength > options.MaxLength)
        {
            throw new InvalidOperationException($"Invalid password policy options: MinLength ({options.MinLength}) cannot be greater than MaxLength ({options.MaxLength}).");
        }

        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            return KyrolusPasswordPolicyResult.Failed(["Password cannot be empty."]);
        }

        if (password.Length < options.MinLength)
        {
            errors.Add($"Password must be at least {options.MinLength} characters long.");
        }

        if (password.Length > options.MaxLength)
        {
            return KyrolusPasswordPolicyResult.Failed([$"Password cannot exceed {options.MaxLength} characters."]);
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSpecial = false;
        var uniqueChars = new HashSet<char>();

        foreach (var c in password)
        {
            uniqueChars.Add(c);
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (!char.IsWhiteSpace(c)) hasSpecial = true;
        }

        if (options.RequireUppercase && !hasUpper)
        {
            errors.Add("Password must contain at least one uppercase letter.");
        }

        if (options.RequireLowercase && !hasLower)
        {
            errors.Add("Password must contain at least one lowercase letter.");
        }

        if (options.RequireDigit && !hasDigit)
        {
            errors.Add("Password must contain at least one digit.");
        }

        if (options.RequireNonAlphanumeric && !hasSpecial)
        {
            errors.Add("Password must contain at least one non-alphanumeric character.");
        }

        if (uniqueChars.Count < options.RequiredUniqueChars)
        {
            errors.Add($"Password must contain at least {options.RequiredUniqueChars} unique characters.");
        }
        else if (password.Length > 3 && uniqueChars.Count == 1)
        {
            errors.Add("Password cannot consist of a single repeated character.");
        }

        if (options.ForbiddenPasswords.Any(fp => string.Equals(fp.Trim(), password.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Password is too common or easily guessable.");
        }

        var score = 0;
        if (password.Length >= 8) score++;
        if (hasUpper && hasLower) score++;
        if (hasDigit) score++;
        if (hasSpecial && password.Length >= 12) score++;

        return errors.Count == 0
            ? KyrolusPasswordPolicyResult.Success(score)
            : KyrolusPasswordPolicyResult.Failed(errors, score);
    }

    public bool IsPasswordPreviouslyUsed(
        string newPassword,
        IEnumerable<string> previousPasswordHashes,
        Func<string, string, bool> verifyPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        ArgumentNullException.ThrowIfNull(previousPasswordHashes);
        ArgumentNullException.ThrowIfNull(verifyPasswordHash);

        foreach (var hash in previousPasswordHashes)
        {
            if (!string.IsNullOrWhiteSpace(hash) && verifyPasswordHash(hash, newPassword))
            {
                return true;
            }
        }

        return false;
    }
}
