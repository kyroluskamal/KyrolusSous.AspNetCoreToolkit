namespace KyrolusSous.Auth.Permissions;

/// <summary>
/// Logical evaluation mode when multiple permissions are required.
/// </summary>
public enum PermissionLogicalOperator
{
    /// <summary>
    /// The user must possess all specified permissions (AND).
    /// </summary>
    And = 0,

    /// <summary>
    /// The user must possess at least one of the specified permissions (OR).
    /// </summary>
    Or = 1
}
