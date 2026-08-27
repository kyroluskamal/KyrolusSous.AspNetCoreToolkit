namespace KyrolusSous.Auth.Mfa;

/// <summary>
/// Service contract for generating and validating single-use emergency recovery codes.
/// </summary>
public interface IKyrolusRecoveryCodeService
{
    /// <summary>
    /// Generates a set of cryptographically random single-use recovery codes.
    /// </summary>
    /// <param name="count">Number of recovery codes to generate, defaults to 10.</param>
    /// <param name="length">Length of each recovery code, defaults to 10 characters.</param>
    IReadOnlyList<string> GenerateRecoveryCodes(int count = 10, int length = 10);

    /// <summary>
    /// Computes a SHA-256 hash of the recovery code for secure database persistence.
    /// </summary>
    string HashRecoveryCode(string code);

    /// <summary>
    /// Verifies a user-supplied recovery code against a stored SHA-256 hash using constant-time comparison.
    /// </summary>
    bool VerifyRecoveryCode(string rawCode, string storedHash);
}
