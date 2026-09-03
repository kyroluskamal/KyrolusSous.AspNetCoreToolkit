namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Defines programmatic authorization requirements for a CQRS request.
/// </summary>
public interface IKyrolusAuthorizedRequest
{
    /// <summary>
    /// Gets the list of roles required to execute the request.
    /// </summary>
    IReadOnlyCollection<string>? RequiredRoles => null;

    /// <summary>
    /// Gets the list of permissions required to execute the request.
    /// </summary>
    IReadOnlyCollection<string>? RequiredPermissions => null;

    /// <summary>
    /// Gets the policy name required to execute the request.
    /// </summary>
    string? RequiredPolicy => null;
}
