using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KyrolusSous.Auth.MultiTenancy;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class IdentityPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Tenant and User keys resolve correctly")]
    public void Tenant_And_User_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Tenant Key
        var services = new ServiceCollection();
        var tenantContext = new KyrolusTenantContext { TenantId = "tenant-corp-99" };
        services.AddSingleton<IKyrolusTenantContext>(tenantContext);
        context.RequestServices = services.BuildServiceProvider();

        KyrolusRateLimitPartitionKeys.GetTenantKey(context).ShouldBe("tenant-corp-99");
        KyrolusRateLimitPartitionKeys.GetTenantAndIpKey(context).ShouldBe("tenant-corp-99:198.51.100.42");

        var emptyTenantContext = new DefaultHttpContext();
        KyrolusRateLimitPartitionKeys.GetTenantKey(emptyTenantContext).ShouldBe("anonymous_tenant");

        // User Key
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-abc-123")
        ], "Bearer"));

        KyrolusRateLimitPartitionKeys.GetUserKey(context).ShouldBe("user_user-abc-123");

        var unauthContext = new DefaultHttpContext();
        unauthContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetUserKey(unauthContext).ShouldBe("ip_198.51.100.42");
    }

    [Fact(DisplayName = "ApiKey and ApiKeyTier resolve correctly")]
    public void ApiKey_And_ApiKeyTier_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        KyrolusRateLimitPartitionKeys.GetApiKey(context).ShouldBe("ip_198.51.100.42");
        context.Request.Headers["X-API-Key"] = "sk_live_secret999";
        KyrolusRateLimitPartitionKeys.GetApiKey(context).ShouldBe("apikey_sk_live_secret999");
        KyrolusRateLimitPartitionKeys.GetApiKey(context, "Custom-Key").ShouldBe("ip_198.51.100.42");

        // Tiers
        KyrolusRateLimitPartitionKeys.GetApiKeyTierKey(context).ShouldBe("apikey_tier_live");
        KyrolusRateLimitPartitionKeys.GetApiKeyTierAndClientKey(context).ShouldBe("apikey_tier_live:198.51.100.42");

        context.Request.Headers["X-API-Key"] = "test_key_abc";
        KyrolusRateLimitPartitionKeys.GetApiKeyTierKey(context).ShouldBe("apikey_tier_test");

        context.Request.Headers["X-API-Key"] = "sbx_sandbox_xyz";
        KyrolusRateLimitPartitionKeys.GetApiKeyTierKey(context).ShouldBe("apikey_tier_sandbox");

        context.Request.Headers["X-API-Key"] = "custom_standard_key";
        KyrolusRateLimitPartitionKeys.GetApiKeyTierKey(context).ShouldBe("apikey_tier_standard");

        var emptyContext = new DefaultHttpContext();
        KyrolusRateLimitPartitionKeys.GetApiKeyTierKey(emptyContext).ShouldBe("apikey_tier_none");
    }

    [Fact(DisplayName = "OAuth ClientId, User Subject Type, and AuthScheme resolve correctly")]
    public void OAuth_Subject_And_AuthScheme_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // OAuth Client ID
        KyrolusRateLimitPartitionKeys.GetOAuthClientIdKey(context).ShouldBe("ip_198.51.100.42");

        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("client_id", "service-billing-agent"),
            new Claim("sub", "service-billing-agent")
        ], "Bearer"));
        KyrolusRateLimitPartitionKeys.GetOAuthClientIdKey(context).ShouldBe("client_service-billing-agent");

        // User Subject Principal Type
        KyrolusRateLimitPartitionKeys.GetUserSubjectTypeKey(context).ShouldBe("subject_machine");
        KyrolusRateLimitPartitionKeys.GetUserSubjectTypeAndClientKey(context).ShouldBe("subject_machine:198.51.100.42");

        var userSubjectContext = new DefaultHttpContext();
        userSubjectContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "usr_12345"),
            new Claim("preferred_username", "alice")
        ], "Bearer"));
        KyrolusRateLimitPartitionKeys.GetUserSubjectTypeKey(userSubjectContext).ShouldBe("subject_user");

        var delegatedContext = new DefaultHttpContext();
        delegatedContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("act", "{\"sub\":\"agent\"}")
        ], "Bearer"));
        KyrolusRateLimitPartitionKeys.GetUserSubjectTypeKey(delegatedContext).ShouldBe("subject_delegated");

        // Auth Scheme
        var authContext = new DefaultHttpContext();
        authContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetAuthSchemeKey(authContext).ShouldBe("auth_anonymous");

        authContext.Request.Headers.Authorization = "Bearer eyJhbGciOi...";
        KyrolusRateLimitPartitionKeys.GetAuthSchemeKey(authContext).ShouldBe("auth_bearer");

        authContext.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";
        KyrolusRateLimitPartitionKeys.GetAuthSchemeKey(authContext).ShouldBe("auth_basic");
        KyrolusRateLimitPartitionKeys.GetAuthSchemeAndClientKey(authContext).ShouldBe("auth_basic:198.51.100.42");
    }

    [Fact(DisplayName = "Client Certificate (mTLS), Roles, Claims, and JWT Issuer resolve correctly")]
    public void Cert_Role_Claim_And_Issuer_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Client Certificate
        KyrolusRateLimitPartitionKeys.GetClientCertificateKey(context).ShouldBe("ip_198.51.100.42");

        using var rsa = RSA.Create(2048);
        var certRequest = new CertificateRequest("CN=KyrolusClientCertTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        context.Connection.ClientCertificate = cert;
        KyrolusRateLimitPartitionKeys.GetClientCertificateKey(context).ShouldBe($"cert_{cert.Thumbprint}");

        // Role Key
        var unauthRoleContext = new DefaultHttpContext();
        KyrolusRateLimitPartitionKeys.GetRoleKey(unauthRoleContext).ShouldBe("role_anonymous");

        var authRoleContext = new DefaultHttpContext();
        authRoleContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "Admin")
        ], "TestAuth"));
        KyrolusRateLimitPartitionKeys.GetRoleKey(authRoleContext).ShouldBe("role_Admin");

        // Claim Key
        var claimContext = new DefaultHttpContext();
        claimContext.Connection.RemoteIpAddress = BaseIp;
        claimContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("org_id", "org_north_america_99")
        ], "TestAuth"));
        KyrolusRateLimitPartitionKeys.GetClaimKey(claimContext, "org_id").ShouldBe("org_id_org_north_america_99");

        // JWT Issuer
        var issContext = new DefaultHttpContext();
        issContext.Connection.RemoteIpAddress = BaseIp;
        issContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iss", "https://auth.kyrolus.com/oauth2/v1")
        ], "TestAuth"));
        KyrolusRateLimitPartitionKeys.GetJwtIssuerKey(issContext).ShouldBe("iss_auth.kyrolus.com/oauth2/v1");
        KyrolusRateLimitPartitionKeys.GetJwtIssuerAndClientKey(issContext).ShouldBe("iss_auth.kyrolus.com/oauth2/v1:198.51.100.42");
    }

    [Fact(DisplayName = "Token Age, Scope, Audience, and Subscription Tier resolve accurately")]
    public void TokenAge_Scope_Audience_And_SubscriptionTier_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        var now = DateTimeOffset.UtcNow;
        var fixedTimeProvider = new FakeTimeProvider(now);

        // Token Age
        var tokenContext = new DefaultHttpContext();
        tokenContext.Connection.RemoteIpAddress = BaseIp;
        tokenContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iat", now.AddMinutes(-5).ToUnixTimeSeconds().ToString())
        ], "Bearer"));
        KyrolusRateLimitPartitionKeys.GetTokenAgeBucketKey(tokenContext, timeProvider: fixedTimeProvider).ShouldBe("token_fresh");

        tokenContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iat", now.AddHours(-2).ToUnixTimeSeconds().ToString())
        ], "Bearer"));
        KyrolusRateLimitPartitionKeys.GetTokenAgeBucketKey(tokenContext, timeProvider: fixedTimeProvider).ShouldBe("token_mature");
        KyrolusRateLimitPartitionKeys.GetTokenAgeBucketAndClientKey(tokenContext, timeProvider: fixedTimeProvider).ShouldBe("token_mature:198.51.100.42");

        // Token Scope
        var scopeContext = new DefaultHttpContext();
        scopeContext.Connection.RemoteIpAddress = BaseIp;
        scopeContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("scope", "read:orders write:orders admin:manage")
        ], "Bearer"));
        KyrolusRateLimitPartitionKeys.GetTokenScopeKey(scopeContext).ShouldBe("scope_admin");
        KyrolusRateLimitPartitionKeys.GetTokenScopeAndClientKey(scopeContext).ShouldBe("scope_admin:198.51.100.42");

        // Token Audience
        var audContext = new DefaultHttpContext();
        audContext.Connection.RemoteIpAddress = BaseIp;
        audContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("aud", "https://api.kyrolus.com/orders")
        ], "Bearer"));
        KyrolusRateLimitPartitionKeys.GetTokenAudienceKey(audContext).ShouldBe("aud_https___api.kyrolus.com_orders");
        KyrolusRateLimitPartitionKeys.GetTokenAudienceAndClientKey(audContext).ShouldBe("aud_https___api.kyrolus.com_orders:198.51.100.42");

        // Subscription Tier
        var tierContext = new DefaultHttpContext();
        tierContext.Connection.RemoteIpAddress = BaseIp;
        tierContext.Request.Headers["X-Subscription-Tier"] = "enterprise";
        KyrolusRateLimitPartitionKeys.GetSubscriptionTierKey(tierContext).ShouldBe("tier_enterprise");
        KyrolusRateLimitPartitionKeys.GetSubscriptionTierAndClientKey(tierContext).ShouldBe("tier_enterprise:198.51.100.42");
    }
}
