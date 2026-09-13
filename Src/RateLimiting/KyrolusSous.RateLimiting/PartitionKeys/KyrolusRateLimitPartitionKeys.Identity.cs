namespace KyrolusSous.RateLimiting;

/// <summary>
/// Authentication, authorization, multi-tenancy, API keys, tokens, and principal subject partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    private static readonly Lazy<Type?> TenantContextType = new(() =>
    {
        var direct = Type.GetType("KyrolusSous.Auth.MultiTenancy.IKyrolusTenantContext, KyrolusSous.Auth.MultiTenancy");
        if (direct != null) return direct;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = assembly.GetType("KyrolusSous.Auth.MultiTenancy.IKyrolusTenantContext");
            if (t != null) return t;
        }
        return null;
    });

    /// <summary>
    /// Resolves a partition key based on the ambient multi-tenant identity.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The resolved tenant identifier, or <c>"anonymous_tenant"</c> if none is resolved.</returns>
    public static string GetTenantKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue("TenantId", out var itemTenant) && itemTenant is string str && !string.IsNullOrWhiteSpace(str))
            return str;

        if (context.RequestServices != null)
        {
            var targetType = TenantContextType.Value;
            if (targetType != null)
            {
                var tenantContext = context.RequestServices.GetService(targetType);
                if (tenantContext != null)
                {
                    var prop = tenantContext.GetType().GetProperty("TenantId");
                    if (prop?.GetValue(tenantContext) is string tid && !string.IsNullOrWhiteSpace(tid))
                        return tid;
                }
            }
        }

        return "anonymous_tenant";
    }

    /// <summary>
    /// Resolves a combined partition key incorporating both Tenant ID and Client IP for fair-share multi-tenant resource throttling.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>A compound key formatted as <c>"{tenant}:{ip}"</c>.</returns>
    public static string GetTenantAndIpKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"{GetTenantKey(context)}:{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on the authenticated user identifier (Name, NameIdentifier, or sub claim),
    /// falling back to the client IP address for unauthenticated requests.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The authenticated user key or IP fallback.</returns>
    public static string GetUserKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User?.Identity is { IsAuthenticated: true })
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? context.User.FindFirst("sub")?.Value
                        ?? context.User.Identity.Name;

            if (!string.IsNullOrWhiteSpace(userId))
                return $"user_{userId}";
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on an API key supplied in an HTTP header (defaults to <c>"X-API-Key"</c>),
    /// falling back to the client IP address if absent.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="headerName">The header name containing the API key. Defaults to <c>"X-API-Key"</c>.</param>
    /// <returns>The API key partition formatted as <c>"apikey_{key}"</c>, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetApiKey(HttpContext context, string headerName = "X-API-Key")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        if (context.Request.Headers.TryGetValue(headerName, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            var key = val.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(key))
                return $"apikey_{key}";
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on the OAuth2 / OIDC client identifier (<c>client_id</c>, <c>azp</c>, or <c>appid</c> claims),
    /// designed for machine-to-machine (M2M) service authentication, falling back to the client IP address.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The client identifier formatted as <c>"client_{clientId}"</c>, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetOAuthClientIdKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User?.Identity is { IsAuthenticated: true })
        {
            var clientId = context.User.FindFirst("client_id")?.Value
                        ?? context.User.FindFirst("azp")?.Value
                        ?? context.User.FindFirst("appid")?.Value;

            if (!string.IsNullOrWhiteSpace(clientId))
                return $"client_{clientId}";
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on the verified mTLS client certificate SHA-256 thumbprint,
    /// providing cryptographic, tamper-proof rate limiting for zero-trust and B2B integrations.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The certificate partition formatted as <c>"cert_{thumbprint}"</c>, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetClientCertificateKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cert = context.Connection.ClientCertificate;
        if (cert is not null)
        {
            var thumbprint = cert.Thumbprint;
            if (!string.IsNullOrWhiteSpace(thumbprint))
                return $"cert_{thumbprint}";
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on the authenticated user's primary role or subscription tier claim (<see cref="ClaimTypes.Role"/> or <c>"role"</c>),
    /// enabling pooled rate limit budgets per user tier (e.g. Free-tier global pool vs Premium dedicated quotas).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="fallbackRole">The role string to return when the user is unauthenticated or has no role claim. Defaults to <c>"anonymous"</c>.</param>
    /// <returns>The role partition key formatted as <c>"role_{roleName}"</c>.</returns>
    public static string GetRoleKey(HttpContext context, string fallbackRole = "anonymous")
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User?.Identity is { IsAuthenticated: true })
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value
                    ?? context.User.FindFirst("role")?.Value;

            if (!string.IsNullOrWhiteSpace(role))
                return $"role_{role}";
        }

        return $"role_{fallbackRole}";
    }

    /// <summary>
    /// Resolves a partition key based on any arbitrary claim in the authenticated user identity (e.g. <c>"organization_id"</c>, <c>"department"</c>, <c>"plan"</c>),
    /// falling back to the client IP address if unauthenticated or the claim is absent.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="claimType">The JWT or identity claim type to extract.</param>
    /// <param name="prefix">Optional prefix for the partition key. If null, the claim type in lowercase is used.</param>
    /// <returns>The claim partition key, or <c>"ip_{clientIp}"</c> fallback.</returns>
    public static string GetClaimKey(HttpContext context, string claimType, string? prefix = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);

        if (context.User?.Identity is { IsAuthenticated: true })
        {
            var claimVal = context.User.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(claimVal))
            {
                var p = prefix ?? claimType.Trim().ToLowerInvariant();
                return $"{p}_{claimVal}";
            }
        }

        return $"ip_{GetClientIpKey(context)}";
    }

    /// <summary>
    /// Resolves a partition key based on the JWT token's <c>iss</c> (Issuer) claim,
    /// enabling realm-level and Identity-Provider-level quota segregation in multi-tenant or multi-realm environments (e.g. Keycloak realms, Azure Entra ID tenants).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The issuer partition key formatted as <c>"iss_{issuer}"</c>, or <c>"iss_unknown"</c>.</returns>
    public static string GetJwtIssuerKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User?.Identity is { IsAuthenticated: true })
        {
            var issuer = context.User.FindFirst("iss")?.Value
                      ?? context.User.FindFirst(ClaimTypes.AuthenticationInstant)?.Issuer;

            if (!string.IsNullOrWhiteSpace(issuer))
            {
                var raw = issuer.Trim();
                if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                    return $"iss_{uri.Authority.ToLowerInvariant()}{uri.AbsolutePath.TrimEnd('/').ToLowerInvariant()}";

                return $"iss_{raw.ToLowerInvariant()}";
            }
        }

        return "iss_unknown";
    }

    /// <summary>
    /// Resolves a compound partition key combining the JWT token issuer with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{issuerKey}:{clientKey}"</c>.</returns>
    public static string GetJwtIssuerAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var issuerKey = GetJwtIssuerKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{issuerKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the HTTP authentication scheme used in the request (<c>"auth_bearer"</c>, <c>"auth_basic"</c>, <c>"auth_apikey"</c>, <c>"auth_digest"</c>, <c>"auth_anonymous"</c>).
    /// Enables strict brute-force and password-spraying rate limiting on Basic Auth while maintaining high capacity for Bearer token APIs.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The authentication scheme partition key.</returns>
    public static string GetAuthSchemeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("Authorization", out var authVal) && !string.IsNullOrWhiteSpace(authVal))
        {
            var raw = authVal.ToString().Trim();
            if (raw.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
                return "auth_bearer";
            if (raw.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
                return "auth_basic";
            if (raw.StartsWith("Digest", StringComparison.OrdinalIgnoreCase))
                return "auth_digest";
            if (raw.StartsWith("Negotiate", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("NTLM", StringComparison.OrdinalIgnoreCase))
                return "auth_negotiate";
        }

        if (context.Request.Headers.ContainsKey("X-API-Key") || context.Request.Headers.ContainsKey("x-api-key"))
            return "auth_apikey";

        return "auth_anonymous";
    }

    /// <summary>
    /// Resolves a compound partition key combining the authentication scheme with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{authSchemeKey}:{clientKey}"</c>.</returns>
    public static string GetAuthSchemeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var authKey = GetAuthSchemeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{authKey}:{clientKey}";
    }

    private static readonly string[] DefaultTierHeaders =
    [
        "X-Subscription-Tier",
        "X-Plan-Tier",
        "X-Tier"
    ];

    /// <summary>
    /// Resolves a partition key based on the client subscription plan or license tier (<c>"tier_{name}"</c>).
    /// Enables edge-level plan partitioning (e.g. <c>"tier_enterprise"</c> vs <c>"tier_community"</c>) without requiring upstream database lookups.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="tierHeaderCandidates">Optional prioritized list of header names to search for subscription tier.</param>
    /// <param name="fallbackTier">The fallback tier when no header matches. Defaults to <c>"community"</c>.</param>
    /// <returns>The subscription tier partition key formatted as <c>"tier_{name}"</c>.</returns>
    public static string GetSubscriptionTierKey(
        HttpContext context,
        string[]? tierHeaderCandidates = null,
        string fallbackTier = "community")
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = tierHeaderCandidates is { Length: > 0 } ? tierHeaderCandidates : DefaultTierHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(raw))
                    return $"tier_{raw}";
            }
        }

        return $"tier_{fallbackTier.Trim().ToLowerInvariant()}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the subscription tier with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="tierHeaderCandidates">Optional prioritized list of header names to search for subscription tier.</param>
    /// <returns>A compound key formatted as <c>"{tierKey}:{clientKey}"</c>.</returns>
    public static string GetSubscriptionTierAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? tierHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tierKey = GetSubscriptionTierKey(context, tierHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{tierKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the remaining lifetime of the authenticated JWT token (<c>"token_near_expiry"</c>, <c>"token_valid"</c>, <c>"token_expired"</c>, <c>"token_anonymous"</c>).
    /// Promotes Zero-Trust token hygiene by throttling clients that burst requests using nearly-expired tokens instead of executing a standard token refresh.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="nearExpiryThresholdSeconds">Threshold in seconds to classify token as near expiry. Defaults to 60 seconds.</param>
    /// <param name="timeProvider">Optional TimeProvider for testability. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The token lifetime classification bucket key.</returns>
    public static string GetTokenLifetimeBucketKey(
        HttpContext context,
        int nearExpiryThresholdSeconds = 60,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nearExpiryThresholdSeconds);

        if (context.User?.Identity is { IsAuthenticated: true })
        {
            var expClaim = context.User.FindFirst("exp")?.Value;
            if (!string.IsNullOrWhiteSpace(expClaim) && long.TryParse(expClaim, CultureInfo.InvariantCulture, out var expEpoch))
            {
                var provider = timeProvider ?? TimeProvider.System;
                var nowEpoch = provider.GetUtcNow().ToUnixTimeSeconds();
                var remaining = expEpoch - nowEpoch;

                if (remaining <= 0)
                    return "token_expired";

                if (remaining <= nearExpiryThresholdSeconds)
                    return "token_near_expiry";

                return "token_valid";
            }
        }

        return "token_anonymous";
    }

    /// <summary>
    /// Resolves a compound partition key combining the JWT token lifetime bucket with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="nearExpiryThresholdSeconds">Threshold in seconds to classify token as near expiry.</param>
    /// <param name="timeProvider">Optional TimeProvider for testability.</param>
    /// <returns>A compound key formatted as <c>"{tokenBucket}:{clientKey}"</c>.</returns>
    public static string GetTokenLifetimeBucketAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int nearExpiryThresholdSeconds = 60,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bucketKey = GetTokenLifetimeBucketKey(context, nearExpiryThresholdSeconds, timeProvider);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{bucketKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the age/freshness of the authenticated token via the <c>"iat"</c> (Issued At) claim.
    /// Enforces zero-trust step-up rate limits or restricts long-lived tokens on sensitive endpoints (<c>"token_fresh"</c>, <c>"token_mature"</c>, <c>"token_clock_skew"</c>, <c>"token_age_unknown"</c>).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="maxFreshSeconds">Maximum age in seconds to classify the token as fresh. Defaults to 300 (5 minutes).</param>
    /// <param name="timeProvider">Optional TimeProvider for testability. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The token freshness partition key.</returns>
    public static string GetTokenAgeBucketKey(
        HttpContext context,
        int maxFreshSeconds = 300,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFreshSeconds);

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "token_age_unknown";

        var iatClaim = user.FindFirst("iat");
        if (iatClaim is null || !long.TryParse(iatClaim.Value, CultureInfo.InvariantCulture, out var iatEpoch))
            return "token_age_unknown";

        var provider = timeProvider ?? TimeProvider.System;
        var nowEpoch = provider.GetUtcNow().ToUnixTimeSeconds();
        var ageSeconds = nowEpoch - iatEpoch;

        if (ageSeconds < 0)
            return "token_clock_skew";

        return ageSeconds <= maxFreshSeconds ? "token_fresh" : "token_mature";
    }

    /// <summary>
    /// Resolves a compound partition key combining the token age freshness bucket with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="maxFreshSeconds">Maximum age in seconds for fresh tokens.</param>
    /// <param name="timeProvider">Optional TimeProvider.</param>
    /// <returns>A compound key formatted as <c>"{ageBucket}:{clientKey}"</c>.</returns>
    public static string GetTokenAgeBucketAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int maxFreshSeconds = 300,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ageKey = GetTokenAgeBucketKey(context, maxFreshSeconds, timeProvider);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{ageKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the environment tier of the client's API Key prefix (e.g. <c>"apikey_tier_live"</c>, <c>"apikey_tier_test"</c>, <c>"apikey_tier_sandbox"</c>, <c>"apikey_tier_standard"</c>, <c>"apikey_tier_none"</c>).
    /// Inspired by Stripe and GitHub key designs, enabling instant zero-database edge partitioning between production, developer sandbox, and test calls.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="apiKeyResolver">Optional resolver to extract the raw API key. Defaults to inspecting <c>"X-API-Key"</c> or query parameter <c>"apiKey"</c>.</param>
    /// <returns>The API Key tier partition key.</returns>
    public static string GetApiKeyTierKey(HttpContext context, Func<HttpContext, string?>? apiKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rawKey = apiKeyResolver?.Invoke(context);
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            if (context.Request.Headers.TryGetValue("X-API-Key", out var hVal) && !string.IsNullOrWhiteSpace(hVal))
                rawKey = hVal.ToString().Trim();
            else if (context.Request.Query.TryGetValue("apiKey", out var qVal) && !string.IsNullOrWhiteSpace(qVal))
                rawKey = qVal.ToString().Trim();
        }

        if (string.IsNullOrWhiteSpace(rawKey))
            return "apikey_tier_none";

        var lowerKey = rawKey.ToLowerInvariant();
        if (lowerKey.StartsWith("sk_live_") || lowerKey.StartsWith("pk_live_") || lowerKey.StartsWith("live_") || lowerKey.StartsWith("prod_"))
            return "apikey_tier_live";

        if (lowerKey.StartsWith("sk_test_") || lowerKey.StartsWith("pk_test_") || lowerKey.StartsWith("test_") || lowerKey.StartsWith("dev_"))
            return "apikey_tier_test";

        if (lowerKey.StartsWith("sandbox_") || lowerKey.StartsWith("sbx_"))
            return "apikey_tier_sandbox";

        return "apikey_tier_standard";
    }

    /// <summary>
    /// Resolves a compound partition key combining the API Key environment tier with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="apiKeyResolver">Optional resolver to extract the raw API key.</param>
    /// <returns>A compound key formatted as <c>"{apiKeyTierKey}:{clientKey}"</c>.</returns>
    public static string GetApiKeyTierAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        Func<HttpContext, string?>? apiKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tierKey = GetApiKeyTierKey(context, apiKeyResolver);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{tierKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the OAuth 2.0 / OpenID Connect principal type (<c>"subject_user"</c>, <c>"subject_machine"</c>, <c>"subject_delegated"</c>, or <c>"subject_anonymous"</c>).
    /// Segregates automated Machine-to-Machine (M2M) batch synchronization from interactive human user sessions in zero-trust architectures.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The subject principal type partition key.</returns>
    public static string GetUserSubjectTypeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "subject_anonymous";

        // Delegated actor token (RFC 8693)
        if (user.HasClaim(c => c.Type == "act" || c.Type == "actor"))
            return "subject_delegated";

        // Machine / M2M service principal (client_credentials grant, or Azure AD appid/idp token)
        var sub = user.FindFirst("sub")?.Value;
        var clientId = user.FindFirst("client_id")?.Value ?? user.FindFirst("azp")?.Value ?? user.FindFirst("appid")?.Value;
        var gty = user.FindFirst("gty")?.Value;

        if (string.Equals(gty, "client_credentials", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(clientId) && string.Equals(sub, clientId, StringComparison.OrdinalIgnoreCase)) ||
            (user.HasClaim(c => c.Type == "appid" || c.Type == "app_id") && !user.HasClaim(c => c.Type == ClaimTypes.Email || c.Type == "email" || c.Type == "name")))
        {
            return "subject_machine";
        }

        return "subject_user";
    }

    /// <summary>
    /// Resolves a compound partition key combining the authenticated subject principal type with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{subjectTypeKey}:{clientKey}"</c>.</returns>
    public static string GetUserSubjectTypeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var typeKey = GetUserSubjectTypeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{typeKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the primary OAuth 2.0 Scope claim (<c>"scope"</c> or <c>"scp"</c>).
    /// Enables tiered rate limiting by granted permission (e.g. <c>"scope_admin"</c>, <c>"scope_write"</c>, <c>"scope_read"</c>, or <c>"scope_none"</c>).
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The OAuth scope partition key.</returns>
    public static string GetTokenScopeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "scope_none";

        var scopeClaim = user.FindFirst("scope")?.Value ?? user.FindFirst("scp")?.Value;
        if (string.IsNullOrWhiteSpace(scopeClaim))
            return "scope_none";

        var scopes = scopeClaim.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (scopes.Length == 0)
            return "scope_none";

        if (scopes.Any(s => s.Contains("admin", StringComparison.OrdinalIgnoreCase)))
            return "scope_admin";
        if (scopes.Any(s => s.Contains("write", StringComparison.OrdinalIgnoreCase) || s.Contains("manage", StringComparison.OrdinalIgnoreCase)))
            return "scope_write";
        if (scopes.Any(s => s.Contains("read", StringComparison.OrdinalIgnoreCase)))
            return "scope_read";

        var first = scopes[0].ToLowerInvariant().Replace(':', '_');
        return $"scope_{first}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the primary OAuth scope with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{scopeKey}:{clientKey}"</c>.</returns>
    public static string GetTokenScopeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scopeKey = GetTokenScopeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{scopeKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the OAuth 2.0 Audience / Resource Indicator claim (<c>"aud"</c>, RFC 8707).
    /// Segregates rate limit quotas across microservice target audiences on shared central gateway endpoints.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The OAuth audience partition key formatted as <c>"aud_{audience}"</c>, or <c>"aud_none"</c>.</returns>
    public static string GetTokenAudienceKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "aud_none";

        var audClaim = user.FindFirst("aud")?.Value;
        if (string.IsNullOrWhiteSpace(audClaim))
            return "aud_none";

        var sanitized = audClaim.Trim().ToLowerInvariant().Replace(':', '_').Replace('/', '_');
        return $"aud_{sanitized}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the OAuth audience with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{audKey}:{clientKey}"</c>.</returns>
    public static string GetTokenAudienceAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var audKey = GetTokenAudienceKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{audKey}:{clientKey}";
    }
}
