using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Auth.Runtime;

/// <summary>
/// The default claims-principal factory. Emits the subject, the login name, the roles, the tenant
/// and any custom claims on the user, gating the profile and email claims on the scopes that were
/// actually granted.
/// </summary>
public sealed class KyrolusClaimsPrincipalFactory(IOptions<KyrolusAuthOptions> options)
    : IKyrolusClaimsPrincipalFactory
{
    private const string ProfileScope = "profile";
    private const string EmailScope = "email";
    private const string PhoneScope = "phone";
    private const string RolesScope = "roles";

    private readonly KyrolusAuthOptions _options = options.Value;

    /// <inheritdoc />
    public ValueTask<ClaimsPrincipal> CreateAsync(
        KyrolusAuthUser user,
        IReadOnlyCollection<string> grantedScopes,
        string authenticationType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(grantedScopes);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Id);

        var identity = new ClaimsIdentity(
            authenticationType,
            nameType: KyrolusAuthConstants.Claims.Name,
            roleType: KyrolusAuthConstants.Claims.Role);

        identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.Sub, user.Id));
        identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.PreferredUsername, user.UserName));

        if (IsGranted(grantedScopes, ProfileScope))
        {
            AddIfPresent(identity, KyrolusAuthConstants.Claims.Name, user.DisplayName ?? user.UserName);
        }

        if (IsGranted(grantedScopes, EmailScope) && !string.IsNullOrWhiteSpace(user.Email))
        {
            identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.Email, user.Email));
            identity.AddClaim(new Claim(
                KyrolusAuthConstants.Claims.EmailVerified,
                user.EmailConfirmed ? "true" : "false",
                ClaimValueTypes.Boolean));
        }

        if (IsGranted(grantedScopes, PhoneScope) && !string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.PhoneNumber, user.PhoneNumber));
            identity.AddClaim(new Claim(
                KyrolusAuthConstants.Claims.PhoneNumberVerified,
                user.PhoneNumberConfirmed ? "true" : "false",
                ClaimValueTypes.Boolean));
        }

        // Roles drive authorization, not disclosure, so they are not withheld unless the
        // application explicitly asked for scope gating and did not grant the roles scope.
        if (!_options.EnforceScopeBasedClaims || IsGranted(grantedScopes, RolesScope) || grantedScopes.Count == 0)
        {
            foreach (var role in user.Roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.Role, role));
                }
            }
        }

        AddIfPresent(identity, KyrolusAuthConstants.Claims.TenantId, user.TenantId);

        foreach (var claim in user.Claims)
        {
            if (!string.IsNullOrWhiteSpace(claim.Key) &&
                !SensitiveClaimKeys.Contains(claim.Key) &&
                !identity.HasClaim(claim.Key, claim.Value))
            {
                identity.AddClaim(new Claim(claim.Key, claim.Value));
            }
        }

        return ValueTask.FromResult(new ClaimsPrincipal(identity));
    }

    private static readonly HashSet<string> SensitiveClaimKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password_hash",
        "PasswordHash",
        "security_stamp",
        "SecurityStamp",
        "concurrency_stamp",
        "ConcurrencyStamp",
        "AspNet.Identity.SecurityStamp"
    };

    private bool IsGranted(IReadOnlyCollection<string> grantedScopes, string scope)
        => !_options.EnforceScopeBasedClaims || grantedScopes.Any(s => string.Equals(s.Trim(), scope, StringComparison.OrdinalIgnoreCase));

    private static void AddIfPresent(ClaimsIdentity identity, string claimType, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            identity.AddClaim(new Claim(claimType, value));
        }
    }
}
