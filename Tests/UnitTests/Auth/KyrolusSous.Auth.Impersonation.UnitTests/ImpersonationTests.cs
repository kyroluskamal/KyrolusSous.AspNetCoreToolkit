using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Impersonation;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Impersonation.UnitTests;

public class ImpersonationTests
{
    private readonly KyrolusImpersonationService _service = new();

    [Fact(DisplayName = "Create Impersonated Principal Embeds Target User And Admin Actor Claims")]
    public void CreateImpersonatedPrincipal_EmbedsTargetUserAndAdminActorClaims()
    {
        var adminIdentity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-999"),
            new Claim(ClaimTypes.Name, "Support SuperAdmin")
        ], "AdminCookie");

        var adminUser = new ClaimsPrincipal(adminIdentity);

        var targetUser = new KyrolusAuthUser
        {
            Id = "customer-123",
            UserName = "johndoe",
            Email = "john@example.com",
            Roles = ["Customer", "Premium"]
        };

        var impersonated = _service.CreateImpersonatedPrincipal(
            targetUser,
            adminUser,
            reason: "Investigating invoice #4567 payment issue");

        impersonated.ShouldNotBeNull();
        impersonated.FindFirst(ClaimTypes.NameIdentifier)?.Value.ShouldBe("customer-123");
        impersonated.FindFirst(ClaimTypes.Name)?.Value.ShouldBe("johndoe");
        impersonated.FindFirst(ClaimTypes.Email)?.Value.ShouldBe("john@example.com");
        impersonated.IsInRole("Customer").ShouldBeTrue();
        impersonated.IsInRole("Premium").ShouldBeTrue();

        _service.IsImpersonating(impersonated).ShouldBeTrue();
        _service.GetOriginalAdminId(impersonated).ShouldBe("admin-999");
        _service.GetOriginalAdminName(impersonated).ShouldBe("Support SuperAdmin");
        _service.GetImpersonationReason(impersonated).ShouldBe("Investigating invoice #4567 payment issue");
    }

    [Fact(DisplayName = "Create Impersonated Principal Throws When Admin Unauthenticated")]
    public void CreateImpersonatedPrincipal_Throws_WhenAdminUnauthenticated()
    {
        var unauthAdmin = new ClaimsPrincipal(new ClaimsIdentity());
        var targetUser = new KyrolusAuthUser { Id = "target-1" };

        Should.Throw<InvalidOperationException>(() =>
            _service.CreateImpersonatedPrincipal(targetUser, unauthAdmin));
    }

    [Fact(DisplayName = "Is Impersonating Returns False For Regular User")]
    public void IsImpersonating_ReturnsFalse_ForRegularUser()
    {
        var regularIdentity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-55")
        ], "Password");

        var regularUser = new ClaimsPrincipal(regularIdentity);

        _service.IsImpersonating(regularUser).ShouldBeFalse();
        _service.GetOriginalAdminId(regularUser).ShouldBeNull();
    }

    [Fact(DisplayName = "Di Registration Add Kyrolus Impersonation Registers Service")]
    public void DiRegistration_AddKyrolusImpersonation_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddKyrolusImpersonation();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusImpersonationService>().ShouldNotBeNull();
    }

    [Fact(DisplayName = "Create Impersonated Principal Throws When Caller Is Already Impersonating")]
    public void CreateImpersonatedPrincipal_Throws_WhenCallerIsAlreadyImpersonating()
    {
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-1"),
            new Claim(KyrolusImpersonationClaimTypes.IsImpersonating, "true")
        ], "AdminCookie"));

        var targetUser = new KyrolusAuthUser { Id = "target-2" };

        var ex = Should.Throw<InvalidOperationException>(() =>
            _service.CreateImpersonatedPrincipal(targetUser, adminUser));

        ex.Message.ShouldContain("nested impersonation");
    }

    [Fact(DisplayName = "Create Impersonated Principal Throws When Admin Impersonates Self")]
    public void CreateImpersonatedPrincipal_Throws_WhenAdminImpersonatesSelf()
    {
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "same-id")
        ], "AdminCookie"));

        var targetUser = new KyrolusAuthUser { Id = "same-id" };

        var ex = Should.Throw<InvalidOperationException>(() =>
            _service.CreateImpersonatedPrincipal(targetUser, adminUser));

        ex.Message.ShouldContain("cannot impersonate themselves");
    }

    [Fact(DisplayName = "Create Impersonated Principal Sanitizes Target User Existing Impersonation Claims")]
    public void CreateImpersonatedPrincipal_SanitizesTargetUserExistingImpersonationClaims()
    {
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-real")
        ], "AdminCookie"));

        var maliciousTarget = new KyrolusAuthUser
        {
            Id = "target-spoof",
            Claims = new Dictionary<string, string>
            {
                [KyrolusImpersonationClaimTypes.ActorId] = "fake-actor-id",
                ["custom_role"] = "finance"
            }
        };

        var principal = _service.CreateImpersonatedPrincipal(maliciousTarget, adminUser);

        _service.GetOriginalAdminId(principal).ShouldBe("admin-real");
        principal.FindFirst("custom_role")?.Value.ShouldBe("finance");
    }

    [Fact(DisplayName = "Is Impersonation Expired Detects Expired Impersonation Session")]
    public void IsImpersonationExpired_DetectsExpiredImpersonationSession()
    {
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-1")
        ], "AdminCookie"));
        var targetUser = new KyrolusAuthUser { Id = "target-1" };

        var principal = _service.CreateImpersonatedPrincipal(targetUser, adminUser);

        var withinWindow = DateTimeOffset.UtcNow.AddMinutes(30);
        _service.IsImpersonationExpired(principal, TimeSpan.FromHours(1), withinWindow).ShouldBeFalse();

        var pastWindow = DateTimeOffset.UtcNow.AddMinutes(65);
        _service.IsImpersonationExpired(principal, TimeSpan.FromHours(1), pastWindow).ShouldBeTrue();

        _service.IsImpersonationExpired(adminUser, TimeSpan.FromHours(1)).ShouldBeFalse();
    }

    [Fact(DisplayName = "Create Impersonated Principal Throws When Admin User Lacks Identifier")]
    public void CreateImpersonatedPrincipal_Throws_WhenAdminUserLacksIdentifier()
    {
        var invalidAdmin = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("some_random_claim", "value")
        ], "AdminCookie"));

        var targetUser = new KyrolusAuthUser { Id = "target-user" };

        Should.Throw<InvalidOperationException>(() =>
            _service.CreateImpersonatedPrincipal(targetUser, invalidAdmin));
    }

    [Fact(DisplayName = "Create Impersonated Principal Truncates Excessive Reason Length")]
    public void CreateImpersonatedPrincipal_TruncatesExcessiveReasonLength()
    {
        var admin = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-1")
        ], "AdminCookie"));

        var targetUser = new KyrolusAuthUser { Id = "target-user" };
        var giantReason = new string('R', 500);

        var principal = _service.CreateImpersonatedPrincipal(targetUser, admin, reason: giantReason);
        var reasonClaim = principal.FindFirst(KyrolusImpersonationClaimTypes.Reason)?.Value;

        reasonClaim.ShouldNotBeNull();
        reasonClaim.Length.ShouldBe(256);
    }

    [Fact(DisplayName = "Create Impersonated Principal Preserves Tenant Id")]
    public void CreateImpersonatedPrincipal_PreservesTenantId()
    {
        var admin = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-1")
        ], "AdminCookie"));

        var targetUser = new KyrolusAuthUser { Id = "target-user", TenantId = "tenant-impersonate" };

        var principal = _service.CreateImpersonatedPrincipal(targetUser, admin);
        principal.FindFirst("tenant_id")?.Value.ShouldBe("tenant-impersonate");
    }
}
