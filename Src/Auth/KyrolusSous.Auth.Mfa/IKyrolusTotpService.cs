namespace KyrolusSous.Auth.Mfa;

/// <summary>
/// Service contract for RFC 6238 Time-based One-Time Password (TOTP) generation and validation.
/// </summary>
public interface IKyrolusTotpService
{
    /// <summary>
    /// Generates a cryptographically secure random Base32 secret key.
    /// </summary>
    /// <param name="byteLength">Length of random bytes, defaults to 20 (160 bits, standard for SHA-1).</param>
    string GenerateSecret(int byteLength = 20);

    /// <summary>
    /// Generates a 6-digit TOTP code for the specified secret key at the given timestamp.
    /// </summary>
    string GenerateCode(string base32Secret, DateTimeOffset? timestamp = null);

    /// <summary>
    /// Validates a user-supplied 6-digit TOTP code against the secret key, allowing for clock drift.
    /// </summary>
    /// <param name="base32Secret">The user's Base32 secret key.</param>
    /// <param name="code">The 6-digit code supplied by the user.</param>
    /// <param name="allowedClockDriftWindows">Allowable 30-second windows before and after current time (default: 1 window = ±30s).</param>
    /// <param name="timestamp">Optional evaluation timestamp, defaults to current UTC time.</param>
    bool ValidateCode(string base32Secret, string code, int allowedClockDriftWindows = 1, DateTimeOffset? timestamp = null);

    /// <summary>
    /// Generates a standard <c>otpauth://</c> URI suitable for QR code generation.
    /// </summary>
    /// <param name="base32Secret">The Base32 secret key.</param>
    /// <param name="accountEmail">The user's email or username.</param>
    /// <param name="issuer">The application or organization name.</param>
    string GenerateQrCodeUri(string base32Secret, string accountEmail, string issuer);
}
