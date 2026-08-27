using System.Security.Claims;

namespace KyrolusSous.Auth.Permissions;

/// <summary>
/// Strategy contract for resolving the complete set of permissions for a user.
/// Can be customized to fetch permissions from database, cache, or external identity providers.
/// </summary>
public interface IKyrolusPermissionResolver
{
    /// <summary>
    /// Resolves the granted permissions for the given <see cref="ClaimsPrincipal"/>.
    /// </summary>
    Task<IReadOnlySet<string>> GetUserPermissionsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default resolver that extracts permissions from claims (e.g., "permission", "permissions", "scope").
/// </summary>
public sealed class KyrolusClaimPermissionResolver : IKyrolusPermissionResolver
{
    private static readonly string[] DefaultPermissionClaimTypes = ["permission", "permissions", "scope"];

    public Task<IReadOnlySet<string>> GetUserPermissionsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user.Identity is not { IsAuthenticated: true })
        {
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claimType in DefaultPermissionClaimTypes)
        {
            foreach (var claim in user.FindAll(claimType))
            {
                if (string.IsNullOrWhiteSpace(claim.Value)) continue;

                // Handle space-separated scopes or single permission strings
                var parts = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var part in parts)
                {
                    permissions.Add(part);
                }
            }
        }

        return Task.FromResult<IReadOnlySet<string>>(permissions);
    }
}
