using Microsoft.AspNetCore.Authorization;

namespace KyrolusSous.Auth.Permissions;

/// <summary>
/// Specifies that the class or method that this attribute is applied to requires the specified permission(s).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class KyrolusRequirePermissionAttribute : AuthorizeAttribute
{
    public IReadOnlyList<string> Permissions { get; }
    public PermissionLogicalOperator LogicalOperator { get; set; } = PermissionLogicalOperator.And;

    public KyrolusRequirePermissionAttribute(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        Permissions = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// Authorization requirement representing one or more permissions evaluated with a logical operator.
/// </summary>
public sealed class KyrolusPermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; }
    public PermissionLogicalOperator LogicalOperator { get; }

    public KyrolusPermissionRequirement(IReadOnlyList<string> permissions, PermissionLogicalOperator logicalOperator = PermissionLogicalOperator.And)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        Permissions = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        LogicalOperator = logicalOperator;
    }
}
