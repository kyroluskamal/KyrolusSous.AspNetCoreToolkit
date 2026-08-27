using System.Security.Claims;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

// Deliberately in the root namespace: a nested "Claims" namespace would shadow the
// OpenIddictConstants.Claims class that every case label below refers to.
namespace KyrolusSous.Auth.OpenIddict;

/// <summary>
/// Decides which token each claim is written into.
/// </summary>
/// <remarks>
/// OpenIddict deliberately refuses to guess: a claim with no destination is dropped from every
/// token, which is the single most common reason a freshly built authorization server issues
/// tokens that appear to contain nothing. This supplies the conventional mapping so an
/// application does not have to write it again.
/// </remarks>
public static class KyrolusClaimDestinations
{
    /// <summary>
    /// Claim types that must never leave the server: they exist to validate a session, and putting
    /// them in a token hands an attacker the material to forge one.
    /// </summary>
    private static readonly string[] NeverEmitted =
    [
        "AspNet.Identity.SecurityStamp",
        "security_stamp",
        "SecurityStamp",
        "password_hash",
        "PasswordHash",
        "concurrency_stamp",
        "ConcurrencyStamp",
    ];

    /// <summary>
    /// Returns the destinations for a single claim, following OpenID Connect scope rules:
    /// the access token always gets the claim, and the identity token only gets it when the
    /// matching scope was granted.
    /// </summary>
    /// <param name="claim">The claim to place.</param>
    /// <param name="principal">The principal the claim belongs to, used to read the granted scopes.</param>
    public static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(principal);

        if (Array.IndexOf(NeverEmitted, claim.Type) >= 0)
        {
            yield break;
        }

        switch (claim.Type)
        {
            case Claims.Name:
            case Claims.PreferredUsername:
            case Claims.GivenName:
            case Claims.FamilyName:
            case Claims.MiddleName:
            case Claims.Nickname:
            case Claims.Picture:
            case Claims.Profile:
            case Claims.Website:
            case Claims.Gender:
            case Claims.Birthdate:
            case Claims.Zoneinfo:
            case Claims.Locale:
            case Claims.UpdatedAt:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Profile))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Email:
            case Claims.EmailVerified:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Email))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.PhoneNumber:
            case Claims.PhoneNumberVerified:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Phone))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Roles))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            default:
                // Application-specific claims (tenant, permissions, ...) belong in the access
                // token, which is the one the API reads. The identity token describes who the
                // user is to the client, and is not an authorization document.
                yield return Destinations.AccessToken;
                yield break;
        }
    }

    /// <summary>
    /// Stamps <see cref="GetDestinations(Claim, ClaimsPrincipal)"/> onto every claim of a principal.
    /// </summary>
    /// <param name="principal">The principal about to be signed in.</param>
    /// <returns>The same principal, for chaining.</returns>
    public static ClaimsPrincipal SetKyrolusDestinations(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, principal));
        }

        return principal;
    }

    /// <summary>
    /// Stamps destinations onto every claim of a principal using a caller-supplied rule.
    /// </summary>
    /// <param name="principal">The principal about to be signed in.</param>
    /// <param name="resolver">Returns the destinations for a claim.</param>
    /// <returns>The same principal, for chaining.</returns>
    public static ClaimsPrincipal SetKyrolusDestinations(
        this ClaimsPrincipal principal,
        Func<Claim, ClaimsPrincipal, IEnumerable<string>> resolver)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(resolver);

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(resolver(claim, principal));
        }

        return principal;
    }
}
