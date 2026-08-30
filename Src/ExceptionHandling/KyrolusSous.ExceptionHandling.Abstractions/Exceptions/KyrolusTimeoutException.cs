namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 504 (Gateway Timeout) exception thrown when an internal operation or downstream dependency times out.
/// </summary>
/// <remarks>
/// Marked with <c>IsTransient = true</c> and <c>ShouldLog = true</c> for server-side alert monitoring.
/// </remarks>
/// <example>
/// <code>
/// throw new KyrolusTimeoutException("The payment gateway did not respond within the 10-second timeout window.");
/// </code>
/// </example>
/// <param name="detail">An optional explanation of the timed-out operation.</param>
/// <param name="innerException">An optional inner exception.</param>
public sealed class KyrolusTimeoutException(string? detail = null, Exception? innerException = null) 
    : KyrolusException(HttpStatusCode.GatewayTimeout, KyrolusErrorCodes.Timeout, "Timeout", detail, null, null, true, true, innerException)
{
}
