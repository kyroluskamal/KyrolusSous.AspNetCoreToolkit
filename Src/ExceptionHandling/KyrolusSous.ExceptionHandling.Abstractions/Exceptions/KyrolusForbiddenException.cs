namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 403 (Forbidden) exception thrown when the authenticated user does not have sufficient permissions to access a resource.
/// </summary>
/// <remarks>
/// Throw this when the user is authenticated (identity known) but not authorized (insufficient role, scope, or tenant access).
/// </remarks>
/// <example>
/// <code>
/// if (!user.IsInRole("Admin"))
///     throw new KyrolusForbiddenException("Only administrators can perform this action.");
/// </code>
/// </example>
/// <param name="detail">An optional explanation of the permission requirement.</param>
/// <param name="innerException">An optional inner exception.</param>
public sealed class KyrolusForbiddenException(string? detail = null, Exception? innerException = null) 
    : KyrolusException(HttpStatusCode.Forbidden, KyrolusErrorCodes.Forbidden, "Forbidden", detail, null, null, false, false, innerException)
{
}
