using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Auth.ApiKey;

public sealed class KyrolusApiKeyAuthenticationHandler : AuthenticationHandler<KyrolusApiKeyAuthenticationOptions>
{
    private readonly IKyrolusApiKeyValidator _validator;

    public KyrolusApiKeyAuthenticationHandler(
        IOptionsMonitor<KyrolusApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IKyrolusApiKeyValidator validator)
        : base(options, logger, encoder)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? providedKey = null;

        // 1. Try Header
        var headerName = string.IsNullOrWhiteSpace(Options.HeaderName) ? "X-Api-Key" : Options.HeaderName;
        if (Request.Headers.TryGetValue(headerName, out var headerValues) && !string.IsNullOrWhiteSpace(headerValues))
        {
            if (headerValues.Count > 1)
            {
                return AuthenticateResult.Fail("Multiple API key headers are not permitted.");
            }

            providedKey = headerValues.ToString().Trim();
        }

        // 2. Try Authorization: ApiKey <key>
        if (string.IsNullOrWhiteSpace(providedKey) && Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            if (authHeader.Count > 1)
            {
                return AuthenticateResult.Fail("Multiple Authorization headers are not permitted.");
            }

            var authHeaderStr = authHeader.ToString().Trim();
            if (authHeaderStr.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
            {
                providedKey = authHeaderStr["ApiKey ".Length..].Trim();
            }
        }

        // 3. Try Query Parameter if enabled
        var queryParam = string.IsNullOrWhiteSpace(Options.QueryParameterName) ? "api_key" : Options.QueryParameterName;
        if (string.IsNullOrWhiteSpace(providedKey) && Options.AllowQueryParameter && Request.Query.TryGetValue(queryParam, out var queryValues))
        {
            if (queryValues.Count > 1)
            {
                return AuthenticateResult.Fail("Multiple API key query parameters are not permitted.");
            }

            providedKey = queryValues.ToString().Trim();
        }

        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return AuthenticateResult.NoResult();
        }

        var validationResult = await _validator.ValidateAsync(providedKey, Context.RequestAborted);
        if (!validationResult.Succeeded || validationResult.ApiKey is null)
        {
            return AuthenticateResult.Fail(validationResult.FailureReason ?? "Invalid API key.");
        }

        var apiKey = validationResult.ApiKey;
        if (!apiKey.IsActive)
        {
            return AuthenticateResult.Fail("API key is inactive or revoked.");
        }

        if (apiKey.ExpiresAtUtc.HasValue && apiKey.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow)
        {
            return AuthenticateResult.Fail("API key has expired.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKey.OwnerId),
            new(ClaimTypes.Name, apiKey.OwnerName),
            new("auth_method", "api_key")
        };

        foreach (var role in apiKey.Roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }

        foreach (var scope in apiKey.Scopes)
        {
            claims.Add(new("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
