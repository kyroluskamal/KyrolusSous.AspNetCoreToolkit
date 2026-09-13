using System.Net;
using System.Security.Claims;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class ZeroTrustPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Authentication Assurance Level, Device Compliance, and Identity Risk Score resolve accurately")]
    public void ZeroTrust_Keys_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // AAL - Anonymous
        KyrolusRateLimitPartitionKeys.GetAuthAssuranceLevelKey(context).ShouldBe("aal_unauthenticated");

        // AAL - ClaimsPrincipal with FIDO2 / WebAuthn
        var claims = new[] { new Claim("amr", "fido"), new Claim("amr", "mfa") };
        var identity = new ClaimsIdentity(claims, "CustomAuth");
        context.User = new ClaimsPrincipal(identity);
        KyrolusRateLimitPartitionKeys.GetAuthAssuranceLevelKey(context).ShouldBe("aal_high");
        KyrolusRateLimitPartitionKeys.GetAuthAssuranceLevelAndClientKey(context).ShouldBe("aal_high:198.51.100.42");

        // AAL - Password only
        var pwdClaims = new[] { new Claim("amr", "pwd") };
        var pwdIdentity = new ClaimsIdentity(pwdClaims, "CustomAuth");
        var pwdContext = new DefaultHttpContext();
        pwdContext.User = new ClaimsPrincipal(pwdIdentity);
        KyrolusRateLimitPartitionKeys.GetAuthAssuranceLevelKey(pwdContext).ShouldBe("aal_low");

        // Device Compliance State
        var devContext = new DefaultHttpContext();
        devContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetDeviceComplianceStateKey(devContext).ShouldBe("device_unmanaged");

        devContext.Request.Headers["X-Device-Compliance"] = "compliant";
        KyrolusRateLimitPartitionKeys.GetDeviceComplianceStateKey(devContext).ShouldBe("device_compliant");
        KyrolusRateLimitPartitionKeys.GetDeviceComplianceStateAndClientKey(devContext).ShouldBe("device_compliant:198.51.100.42");

        devContext.Request.Headers["X-Device-Compliance"] = "quarantined";
        KyrolusRateLimitPartitionKeys.GetDeviceComplianceStateKey(devContext).ShouldBe("device_noncompliant");

        // Identity Risk Score
        var riskContext = new DefaultHttpContext();
        riskContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetIdentityRiskScoreKey(riskContext).ShouldBe("identity_risk_none");

        riskContext.Request.Headers["X-Identity-Risk-Score"] = "15";
        KyrolusRateLimitPartitionKeys.GetIdentityRiskScoreKey(riskContext).ShouldBe("identity_risk_low");

        riskContext.Request.Headers["X-Identity-Risk-Score"] = "50";
        KyrolusRateLimitPartitionKeys.GetIdentityRiskScoreKey(riskContext).ShouldBe("identity_risk_medium");

        riskContext.Request.Headers["X-Identity-Risk-Score"] = "85";
        KyrolusRateLimitPartitionKeys.GetIdentityRiskScoreKey(riskContext).ShouldBe("identity_risk_high");
        KyrolusRateLimitPartitionKeys.GetIdentityRiskScoreAndClientKey(riskContext).ShouldBe("identity_risk_high:198.51.100.42");
    }
}
