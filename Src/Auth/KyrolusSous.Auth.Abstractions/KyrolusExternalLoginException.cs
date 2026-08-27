namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Thrown when an external login is refused - by the provider options, or by the application
/// <see cref="IKyrolusExternalLoginHandler"/>.
/// </summary>
/// <remarks>
/// <para>
/// Refusal is signalled by throwing rather than by returning, because that is the only reliable
/// way to abort a remote sign-in. <c>RemoteAuthenticationHandler</c> ignores a failed
/// <c>AuthenticateResult</c> set from the ticket-received event and signs the user in regardless;
/// an exception raised while the ticket is being created is caught by the handler and routed
/// through <c>OnRemoteFailure</c>, which is the documented place to turn it into a redirect.
/// </para>
/// <example>
/// <code>
/// services.AddKyrolusGoogleAuth(
///     options => { /* ... */ },
///     google => google.Events.OnRemoteFailure = context =>
///     {
///         var code = (context.Failure as KyrolusExternalLoginException)?.ErrorCode;
///         context.Response.Redirect($"/login?error={code}");
///         context.HandleResponse();
///         return Task.CompletedTask;
///     });
/// </code>
/// </example>
/// </remarks>
public sealed class KyrolusExternalLoginException : Exception
{
    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="providerName">The provider that authenticated the user.</param>
    /// <param name="errorCode">A code from <see cref="KyrolusAuthConstants.Errors"/>.</param>
    /// <param name="message">A human-readable reason.</param>
    public KyrolusExternalLoginException(string providerName, string errorCode, string message)
        : base(message)
    {
        ProviderName = providerName;
        ErrorCode = errorCode;
    }

    /// <summary>Gets the provider that authenticated the user.</summary>
    public string ProviderName { get; } = "";

    /// <summary>Gets the error code, from <see cref="KyrolusAuthConstants.Errors"/>.</summary>
    public string ErrorCode { get; } = "";
}
