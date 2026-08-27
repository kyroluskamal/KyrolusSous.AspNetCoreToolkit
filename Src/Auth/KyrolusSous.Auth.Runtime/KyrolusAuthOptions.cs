using System.Security.Cryptography;

namespace KyrolusSous.Auth.Runtime;

/// <summary>
/// Options for the default Kyrolus auth runtime: password hashing parameters and the sign-in policy.
/// </summary>
public sealed class KyrolusAuthOptions
{
    /// <summary>
    /// Gets or sets the PBKDF2 iteration count used for new password hashes.
    /// Defaults to 210,000, the OWASP recommendation for PBKDF2-HMAC-SHA512.
    /// </summary>
    /// <remarks>
    /// Raising this only affects newly written hashes. Existing hashes carry their own iteration
    /// count and keep verifying; they are reported as
    /// <see cref="Abstractions.KyrolusPasswordVerificationResult.SuccessRehashNeeded"/> so the
    /// application can upgrade them on the next successful sign-in.
    /// </remarks>
    public int Pbkdf2Iterations { get; set; } = 210_000;

    /// <summary>
    /// Gets or sets the hash algorithm used for new password hashes. Defaults to SHA-512.
    /// </summary>
    public HashAlgorithmName Pbkdf2HashAlgorithm { get; set; } = HashAlgorithmName.SHA512;

    /// <summary>Gets or sets the salt size in bytes for new password hashes. Defaults to 16.</summary>
    public int SaltSizeInBytes { get; set; } = 16;

    /// <summary>Gets or sets the derived key size in bytes for new password hashes. Defaults to 32.</summary>
    public int KeySizeInBytes { get; set; } = 32;

    /// <summary>
    /// Gets or sets whether a user may sign in with their email address as well as their
    /// login name. Defaults to <c>true</c>.
    /// </summary>
    public bool AllowSignInWithEmail { get; set; } = true;

    /// <summary>
    /// Gets or sets whether an unconfirmed email address blocks sign-in. Defaults to <c>false</c>.
    /// </summary>
    public bool RequireConfirmedEmail { get; set; }

    /// <summary>
    /// Gets or sets how many consecutive failures lock an account out. Defaults to 5.
    /// Only enforced when an <see cref="Abstractions.IKyrolusAuthUserLockoutStore"/> is registered.
    /// </summary>
    public int MaxFailedAccessAttempts { get; set; } = 5;

    /// <summary>Gets or sets how long a lockout lasts. Defaults to 5 minutes.</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether the granted scopes gate which claims reach the principal - emitting
    /// <c>email</c> only under the <c>email</c> scope, profile claims only under <c>profile</c>.
    /// Defaults to <c>true</c>, which is what OpenID Connect requires.
    /// </summary>
    public bool EnforceScopeBasedClaims { get; set; } = true;
}
