namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 409 (Conflict) exception thrown when a request conflicts with the current state of the server or database.
/// </summary>
/// <remarks>
/// Common use cases include unique constraint violations (duplicate email, username, phone), or conflicting edit states.
/// </remarks>
/// <example>
/// <code>
/// if (await userStore.EmailExistsAsync(email))
///     throw new KyrolusConflictException("Email Conflict", $"The email address '{email}' is already in use.");
/// </code>
/// </example>
/// <param name="title">A short title describing the conflict.</param>
/// <param name="detail">An optional detailed explanation of the conflicting state.</param>
/// <param name="innerException">An optional inner exception.</param>
public sealed class KyrolusConflictException(string title, string? detail = null, Exception? innerException = null) 
    : KyrolusException(HttpStatusCode.Conflict, KyrolusErrorCodes.Conflict, title, detail, null, null, false, false, innerException)
{
}
