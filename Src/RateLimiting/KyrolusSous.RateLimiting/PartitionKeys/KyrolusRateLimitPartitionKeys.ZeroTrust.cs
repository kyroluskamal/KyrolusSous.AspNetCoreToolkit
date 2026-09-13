using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.RateLimiting;

/// <summary>
/// Zero-Trust Architecture, NIST Authentication Assurance Levels (AAL), Device MDM Compliance, and Identity Risk Score partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    private static readonly string[] DefaultAssuranceHeaders =
    [
        "X-Auth-Assurance",
        "X-AMR",
        "X-AAL",
        "X-Authentication-Method"
    ];

    /// <summary>
    /// Resolves a partition key based on NIST SP 800-63B Authentication Assurance Level (AAL) (<c>"aal_high"</c>, <c>"aal_medium"</c>, <c>"aal_low"</c>, <c>"aal_unauthenticated"</c>).
    /// Awards elevated rate limit quotas to cryptographically verified FIDO2/WebAuthn hardware tokens (AAL3) while restricting single-factor or anonymous endpoints.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="assuranceHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The authentication assurance level partition key.</returns>
    public static string GetAuthAssuranceLevelKey(HttpContext context, string[]? assuranceHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Inspect ClaimsPrincipal AMR / ACR claims
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var amrClaims = user.FindAll("amr").Select(c => c.Value.ToLowerInvariant()).ToList();
            var acrClaim = user.FindFirst("acr")?.Value.ToLowerInvariant();
            var aalClaim = user.FindFirst("aal")?.Value.ToLowerInvariant();

            if (amrClaims.Any(c => c.Contains("fido") || c.Contains("webauthn") || c.Contains("hwk") || c.Contains("passkey")) ||
                acrClaim is "aal3" || aalClaim is "aal3")
            {
                return "aal_high";
            }

            if (amrClaims.Any(c => c.Contains("mfa") || c.Contains("otp") || c.Contains("sms") || c.Contains("totp")) ||
                acrClaim is "aal2" || aalClaim is "aal2")
            {
                return "aal_medium";
            }

            return "aal_low";
        }

        // 2. Inspect Edge Gateway Headers
        var candidates = assuranceHeaderCandidates is { Length: > 0 } ? assuranceHeaderCandidates : DefaultAssuranceHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (raw.Contains("fido") || raw.Contains("webauthn") || raw.Contains("aal3") || raw.Contains("passkey"))
                    return "aal_high";
                if (raw.Contains("mfa") || raw.Contains("otp") || raw.Contains("aal2") || raw.Contains("totp"))
                    return "aal_medium";
                if (raw.Contains("pwd") || raw.Contains("password") || raw.Contains("aal1"))
                    return "aal_low";
            }
        }

        return "aal_unauthenticated";
    }

    /// <summary>
    /// Resolves a compound partition key combining the authentication assurance level with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="assuranceHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{aalKey}:{clientKey}"</c>.</returns>
    public static string GetAuthAssuranceLevelAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? assuranceHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var aalKey = GetAuthAssuranceLevelKey(context, assuranceHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{aalKey}:{clientKey}";
    }

    private static readonly string[] DefaultComplianceHeaders =
    [
        "X-Device-Compliance",
        "X-MDM-Compliance",
        "X-Intune-Compliant",
        "X-Endpoint-Compliant"
    ];

    /// <summary>
    /// Resolves a partition key based on corporate MDM device compliance status (<c>"device_compliant"</c>, <c>"device_noncompliant"</c>, <c>"device_unmanaged"</c>).
    /// Protects internal APIs by prioritizing compliant corporate managed devices (Microsoft Intune, Jamf, Workspace ONE) over unmanaged BYOD endpoints.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="complianceHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The device compliance partition key.</returns>
    public static string GetDeviceComplianceStateKey(HttpContext context, string[]? complianceHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = complianceHeaderCandidates is { Length: > 0 } ? complianceHeaderCandidates : DefaultComplianceHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (raw is "compliant" or "managed" or "true" or "1")
                    return "device_compliant";
                if (raw is "noncompliant" or "uncompliant" or "quarantined" or "false" or "0")
                    return "device_noncompliant";
            }
        }

        return "device_unmanaged";
    }

    /// <summary>
    /// Resolves a compound partition key combining the device compliance status with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="complianceHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{deviceStateKey}:{clientKey}"</c>.</returns>
    public static string GetDeviceComplianceStateAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        string[]? complianceHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stateKey = GetDeviceComplianceStateKey(context, complianceHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{stateKey}:{clientKey}";
    }

    private static readonly string[] DefaultIdentityRiskHeaders =
    [
        "X-Identity-Risk-Score",
        "X-User-Risk-Level",
        "X-Risk-Score",
        "X-Session-Risk"
    ];

    /// <summary>
    /// Resolves a partition key based on dynamic identity threat risk scores (<c>"identity_risk_high"</c>, <c>"identity_risk_medium"</c>, <c>"identity_risk_low"</c>, <c>"identity_risk_none"</c>).
    /// Injected by Identity Providers (e.g. Entra ID Protection, Okta Identity Threat Protection, Ping Identity) to throttle compromised user accounts.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="highRiskThreshold">Score above which a user is classified as high risk. Defaults to 70.</param>
    /// <param name="mediumRiskThreshold">Score above which a user is classified as medium risk. Defaults to 30.</param>
    /// <param name="riskHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The identity risk partition key.</returns>
    public static string GetIdentityRiskScoreKey(
        HttpContext context,
        int highRiskThreshold = 70,
        int mediumRiskThreshold = 30,
        string[]? riskHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(mediumRiskThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(highRiskThreshold, mediumRiskThreshold);

        var candidates = riskHeaderCandidates is { Length: > 0 } ? riskHeaderCandidates : DefaultIdentityRiskHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                var raw = val.ToString().Trim().ToLowerInvariant();
                if (int.TryParse(raw, CultureInfo.InvariantCulture, out var score))
                {
                    if (score >= highRiskThreshold)
                        return "identity_risk_high";
                    if (score >= mediumRiskThreshold)
                        return "identity_risk_medium";

                    return "identity_risk_low";
                }

                if (raw is "high" or "critical" or "severe") return "identity_risk_high";
                if (raw is "medium" or "moderate") return "identity_risk_medium";
                if (raw is "low") return "identity_risk_low";
            }
        }

        return "identity_risk_none";
    }

    /// <summary>
    /// Resolves a compound partition key combining the identity risk score with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="highRiskThreshold">Threshold for high risk.</param>
    /// <param name="mediumRiskThreshold">Threshold for medium risk.</param>
    /// <param name="riskHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{riskKey}:{clientKey}"</c>.</returns>
    public static string GetIdentityRiskScoreAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int highRiskThreshold = 70,
        int mediumRiskThreshold = 30,
        string[]? riskHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var riskKey = GetIdentityRiskScoreKey(context, highRiskThreshold, mediumRiskThreshold, riskHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{riskKey}:{clientKey}";
    }
}
