using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Impersonation;

/// <summary>
/// Service contract for creating and inspecting impersonated user identities for support diagnostics.
/// </summary>
public interface IKyrolusImpersonationService
{
    ClaimsPrincipal CreateImpersonatedPrincipal(
        KyrolusAuthUser targetUser,
        ClaimsPrincipal adminUser,
        string reason = "");

    bool IsImpersonating(ClaimsPrincipal principal);

    string? GetOriginalAdminId(ClaimsPrincipal principal);

    string? GetOriginalAdminName(ClaimsPrincipal principal);

    string? GetImpersonationReason(ClaimsPrincipal principal);

    /// <summary>
    /// Checks whether an active impersonation session has exceeded its maximum permitted duration.
    /// </summary>
    bool IsImpersonationExpired(ClaimsPrincipal principal, TimeSpan maxDuration, DateTimeOffset? now = null);
}

/// <summary>
/// High-performance implementation of <see cref="IKyrolusImpersonationService"/>.
/// </summary>
public sealed class KyrolusImpersonationService : IKyrolusImpersonationService
{
    public ClaimsPrincipal CreateImpersonatedPrincipal(
        KyrolusAuthUser targetUser,
        ClaimsPrincipal adminUser,
        string reason = "")
    {
        ArgumentNullException.ThrowIfNull(targetUser);
        ArgumentNullException.ThrowIfNull(adminUser);

        if (adminUser.Identity is not { IsAuthenticated: true })
        {
            throw new InvalidOperationException("The admin attempting impersonation must be authenticated.");
        }

        if (IsImpersonating(adminUser))
        {
            throw new InvalidOperationException("Cannot initiate nested impersonation. The caller is already impersonating another identity.");
        }

        var adminId = adminUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? adminUser.FindFirst("sub")?.Value
                   ?? adminUser.Identity.Name;

        if (string.IsNullOrWhiteSpace(adminId))
        {
            throw new InvalidOperationException("The administrator attempting impersonation must have a valid identifier (NameIdentifier or sub).");
        }

        if (string.Equals(adminId, targetUser.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An administrator cannot impersonate themselves.");
        }

        var adminName = adminUser.FindFirst(ClaimTypes.Name)?.Value
                     ?? adminUser.FindFirst("name")?.Value
                     ?? adminId;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, targetUser.Id),
            new(ClaimTypes.Name, targetUser.UserName),
            new(KyrolusImpersonationClaimTypes.IsImpersonating, "true"),
            new(KyrolusImpersonationClaimTypes.ActorId, adminId),
            new(KyrolusImpersonationClaimTypes.ActorName, adminName),
            new(KyrolusImpersonationClaimTypes.ImpersonatedAt, DateTimeOffset.UtcNow.ToString("O"))
        };

        if (!string.IsNullOrEmpty(targetUser.Email))
        {
            claims.Add(new(ClaimTypes.Email, targetUser.Email));
        }

        if (!string.IsNullOrEmpty(targetUser.TenantId))
        {
            claims.Add(new("tenant_id", targetUser.TenantId));
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            var cleanReason = reason.Trim();
            var boundedReason = cleanReason.Length > 256 ? cleanReason[..256] : cleanReason;
            claims.Add(new(KyrolusImpersonationClaimTypes.Reason, boundedReason));
        }

        foreach (var role in targetUser.Roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }

        foreach (var (key, value) in targetUser.Claims)
        {
            if (!string.Equals(key, KyrolusImpersonationClaimTypes.IsImpersonating, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, KyrolusImpersonationClaimTypes.ActorId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, KyrolusImpersonationClaimTypes.ActorName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, KyrolusImpersonationClaimTypes.Reason, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, KyrolusImpersonationClaimTypes.ImpersonatedAt, StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new(key, value));
            }
        }

        var identity = new ClaimsIdentity(claims, "KyrolusImpersonation");
        return new ClaimsPrincipal(identity);
    }

    public bool IsImpersonating(ClaimsPrincipal principal)
    {
        return principal?.HasClaim(KyrolusImpersonationClaimTypes.IsImpersonating, "true") == true;
    }

    public string? GetOriginalAdminId(ClaimsPrincipal principal)
    {
        return principal?.FindFirst(KyrolusImpersonationClaimTypes.ActorId)?.Value;
    }

    public string? GetOriginalAdminName(ClaimsPrincipal principal)
    {
        return principal?.FindFirst(KyrolusImpersonationClaimTypes.ActorName)?.Value;
    }

    public string? GetImpersonationReason(ClaimsPrincipal principal)
    {
        return principal?.FindFirst(KyrolusImpersonationClaimTypes.Reason)?.Value;
    }

    public bool IsImpersonationExpired(ClaimsPrincipal principal, TimeSpan maxDuration, DateTimeOffset? now = null)
    {
        if (principal is null || !IsImpersonating(principal))
        {
            return false;
        }

        var impersonatedAtStr = principal.FindFirst(KyrolusImpersonationClaimTypes.ImpersonatedAt)?.Value;
        if (string.IsNullOrEmpty(impersonatedAtStr))
        {
            return true;
        }

        DateTimeOffset impersonatedAt;
        if (DateTimeOffset.TryParse(impersonatedAtStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedIso))
        {
            impersonatedAt = parsedIso;
        }
        else if (long.TryParse(impersonatedAtStr, out var unixSeconds))
        {
            impersonatedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        else
        {
            return true;
        }

        var currentTime = now ?? DateTimeOffset.UtcNow;
        return (currentTime - impersonatedAt) > maxDuration;
    }
}
