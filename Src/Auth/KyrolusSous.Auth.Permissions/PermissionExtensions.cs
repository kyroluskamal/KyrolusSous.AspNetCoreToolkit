using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Auth.Permissions;

public static class PermissionExtensions
{
    /// <summary>
    /// Enforces that the endpoint caller must possess the specified permission.
    /// </summary>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return builder.AddEndpointFilter(new KyrolusPermissionEndpointFilter([permission], PermissionLogicalOperator.And));
    }

    /// <summary>
    /// Enforces that the endpoint caller must possess the specified permissions according to the logical operator.
    /// </summary>
    public static RouteHandlerBuilder RequirePermissions(
        this RouteHandlerBuilder builder,
        PermissionLogicalOperator logicalOperator,
        params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (permissions.Length == 0)
        {
            throw new ArgumentException("At least one permission must be specified.", nameof(permissions));
        }

        return builder.AddEndpointFilter(new KyrolusPermissionEndpointFilter(permissions, logicalOperator));
    }
}
