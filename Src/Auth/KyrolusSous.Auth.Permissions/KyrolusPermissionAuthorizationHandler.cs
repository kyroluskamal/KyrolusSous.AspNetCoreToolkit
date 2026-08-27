using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Permissions;

public sealed class KyrolusPermissionAuthorizationHandler : AuthorizationHandler<KyrolusPermissionRequirement>
{
    private readonly IKyrolusPermissionResolver _permissionResolver;

    public KyrolusPermissionAuthorizationHandler(IKyrolusPermissionResolver permissionResolver)
    {
        _permissionResolver = permissionResolver ?? throw new ArgumentNullException(nameof(permissionResolver));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        KyrolusPermissionRequirement requirement)
    {
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            return;
        }

        if (requirement.Permissions.Count == 0)
        {
            return;
        }

        var userPermissions = await _permissionResolver.GetUserPermissionsAsync(context.User);

        var isAuthorized = requirement.LogicalOperator switch
        {
            PermissionLogicalOperator.And => requirement.Permissions.All(p => userPermissions.Any(u => MatchesPermission(u, p))),
            PermissionLogicalOperator.Or => requirement.Permissions.Any(p => userPermissions.Any(u => MatchesPermission(u, p))),
            _ => false
        };

        if (isAuthorized)
        {
            context.Succeed(requirement);
        }
    }

    internal static bool MatchesPermission(string userPerm, string requiredPerm)
    {
        if (string.IsNullOrWhiteSpace(userPerm) || string.IsNullOrWhiteSpace(requiredPerm))
        {
            return false;
        }

        var u = userPerm.Trim();
        var p = requiredPerm.Trim();

        if (u.Contains("..") || p.Contains("..") || u.Contains("::") || p.Contains("::"))
        {
            return false;
        }

        if (string.Equals(u, p, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (u == "*")
        {
            return true;
        }

        if (u.EndsWith(".*", StringComparison.OrdinalIgnoreCase) &&
            p.StartsWith(u[..^1], StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (u.EndsWith(":*", StringComparison.OrdinalIgnoreCase) &&
            p.StartsWith(u[..^1], StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

public sealed class KyrolusPermissionEndpointFilter : IEndpointFilter
{
    private readonly IReadOnlyList<string> _permissions;
    private readonly PermissionLogicalOperator _logicalOperator;

    public KyrolusPermissionEndpointFilter(IReadOnlyList<string> permissions, PermissionLogicalOperator logicalOperator)
    {
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _logicalOperator = logicalOperator;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (httpContext.User.Identity is not { IsAuthenticated: true })
        {
            return Results.Unauthorized();
        }

        if (_permissions.Count == 0)
        {
            return Results.Forbid();
        }

        var resolver = httpContext.RequestServices.GetRequiredService<IKyrolusPermissionResolver>();
        var userPermissions = await resolver.GetUserPermissionsAsync(httpContext.User, httpContext.RequestAborted);

        var isAuthorized = _logicalOperator switch
        {
            PermissionLogicalOperator.And => _permissions.All(p => userPermissions.Any(u => KyrolusPermissionAuthorizationHandler.MatchesPermission(u, p))),
            PermissionLogicalOperator.Or => _permissions.Any(p => userPermissions.Any(u => KyrolusPermissionAuthorizationHandler.MatchesPermission(u, p))),
            _ => false
        };

        if (!isAuthorized)
        {
            return Results.Forbid();
        }

        return await next(context);
    }
}
