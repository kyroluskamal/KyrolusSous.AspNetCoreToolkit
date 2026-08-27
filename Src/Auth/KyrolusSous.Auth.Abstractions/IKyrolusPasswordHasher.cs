namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Hashes and verifies passwords. Abstracted so an application can keep whatever scheme its
/// existing user table already uses (ASP.NET Identity v3, bcrypt, Argon2, ...) instead of being
/// forced onto ours.
/// </summary>
public interface IKyrolusPasswordHasher
{
    /// <summary>Hashes a plaintext password into a self-describing, storable string.</summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a plaintext password against a stored hash.
    /// </summary>
    /// <param name="hashedPassword">The stored hash.</param>
    /// <param name="providedPassword">The plaintext password supplied by the user.</param>
    KyrolusPasswordVerificationResult Verify(string hashedPassword, string providedPassword);
}

/// <summary>
/// The outcome of a password verification.
/// </summary>
public enum KyrolusPasswordVerificationResult
{
    /// <summary>The password does not match.</summary>
    Failed = 0,

    /// <summary>The password matches.</summary>
    Success = 1,

    /// <summary>
    /// The password matches, but the stored hash uses outdated parameters. Re-hash and store it
    /// on the next successful sign-in.
    /// </summary>
    SuccessRehashNeeded = 2,
}
