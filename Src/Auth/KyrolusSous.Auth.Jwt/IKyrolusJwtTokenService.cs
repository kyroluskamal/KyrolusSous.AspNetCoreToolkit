using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Jwt;

/// <summary>
/// Service contract for generating, signing, and validating JSON Web Tokens.
/// </summary>
public interface IKyrolusJwtTokenService
{
    /// <summary>
    /// Generates a signed JWT access token for the specified user.
    /// </summary>
    string GenerateAccessToken(KyrolusAuthUser user, IEnumerable<Claim>? additionalClaims = null);

    /// <summary>
    /// Generates a cryptographically secure random refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Computes a SHA-256 hash of the refresh token for secure database storage.
    /// </summary>
    string HashRefreshToken(string refreshToken);

    /// <summary>
    /// Validates an access token asynchronously and returns the ClaimsPrincipal if valid.
    /// </summary>
    Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an access token and returns the ClaimsPrincipal if valid.
    /// </summary>
    ClaimsPrincipal? ValidateAccessToken(string token);

    /// <summary>
    /// Validates a raw refresh token against a stored SHA-256 hash.
    /// </summary>
    bool VerifyRefreshToken(string rawRefreshToken, string storedHash);
}
