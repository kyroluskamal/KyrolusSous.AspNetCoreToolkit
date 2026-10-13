using Microsoft.AspNetCore.Http;

namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Defines a contract for validating edge API keys at the API gateway perimeter.
/// Implementations can query databases, distributed caches (Redis), or external secret managers.
/// </summary>
public interface IKyrolusApiKeyValidator
{
    /// <summary>
    /// Asynchronously validates an incoming API key for the current HTTP request.
    /// </summary>
    /// <param name="apiKey">The API key extracted from the request header or query parameter.</param>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the API key is valid and authorized; otherwise, <c>false</c>.</returns>
    ValueTask<bool> ValidateAsync(string apiKey, HttpContext context, CancellationToken cancellationToken = default);
}
