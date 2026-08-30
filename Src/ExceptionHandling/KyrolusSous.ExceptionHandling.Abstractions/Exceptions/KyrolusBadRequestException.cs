namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 400 (Bad Request) exception thrown when the client sends malformed, invalid, or unprocessable data.
/// </summary>
/// <remarks>
/// Use this when a request cannot be processed due to client error, invalid arguments, or malformed input payloads.
/// </remarks>
/// <example>
/// <code>
/// if (request.StartDate > request.EndDate)
///     throw new KyrolusBadRequestException("Invalid Date Range", "StartDate cannot be later than EndDate.");
/// </code>
/// </example>
/// <param name="title">A short summary of the bad request.</param>
/// <param name="detail">An optional detailed explanation.</param>
/// <param name="innerException">An optional inner exception.</param>
public sealed class KyrolusBadRequestException(string title, string? detail = null, Exception? innerException = null) 
    : KyrolusException(HttpStatusCode.BadRequest, KyrolusErrorCodes.BadRequest, title, detail, null, null, false, false, innerException)
{
}
