using System.Security.Claims;
using KyrolusSous.Auth.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Permissions.UnitTests;

public class PermissionAuthorizationTests
{
    private readonly KyrolusClaimPermissionResolver _resolver = new();

    [Fact(DisplayName = "Claim Resolver Extracts Permissions And Scopes")]
    public async Task ClaimResolver_ExtractsPermissionsAndScopes()
    {
        var identity = new ClaimsIdentity([
            new Claim("permission", "orders.read"),
            new Claim("permission", "orders.create"),
            new Claim("scope", "invoices.read reports.view")
        ], "TestAuth");

        var principal = new ClaimsPrincipal(identity);

        var permissions = await _resolver.GetUserPermissionsAsync(principal);

        permissions.ShouldContain("orders.read");
        permissions.ShouldContain("orders.create");
        permissions.ShouldContain("invoices.read");
        permissions.ShouldContain("reports.view");
        permissions.ShouldNotContain("orders.delete");
    }

    [Fact(DisplayName = "Authorization Handler Succeeds When All Permissions Present In And Mode")]
    public async Task AuthorizationHandler_Succeeds_WhenAllPermissionsPresent_InAndMode()
    {
        var handler = new KyrolusPermissionAuthorizationHandler(_resolver);
        var identity = new ClaimsIdentity([
            new Claim("permission", "orders.read"),
            new Claim("permission", "orders.write")
        ], "TestAuth");

        var user = new ClaimsPrincipal(identity);
        var requirement = new KyrolusPermissionRequirement(["orders.read", "orders.write"], PermissionLogicalOperator.And);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact(DisplayName = "Authorization Handler Fails When One Permission Missing In And Mode")]
    public async Task AuthorizationHandler_Fails_WhenOnePermissionMissing_InAndMode()
    {
        var handler = new KyrolusPermissionAuthorizationHandler(_resolver);
        var identity = new ClaimsIdentity([
            new Claim("permission", "orders.read")
        ], "TestAuth");

        var user = new ClaimsPrincipal(identity);
        var requirement = new KyrolusPermissionRequirement(["orders.read", "orders.delete"], PermissionLogicalOperator.And);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact(DisplayName = "Authorization Handler Succeeds When Any Permission Present In Or Mode")]
    public async Task AuthorizationHandler_Succeeds_WhenAnyPermissionPresent_InOrMode()
    {
        var handler = new KyrolusPermissionAuthorizationHandler(_resolver);
        var identity = new ClaimsIdentity([
            new Claim("permission", "orders.read")
        ], "TestAuth");

        var user = new ClaimsPrincipal(identity);
        var requirement = new KyrolusPermissionRequirement(["orders.read", "orders.admin"], PermissionLogicalOperator.Or);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact(DisplayName = "Authorization Handler Does Not Succeed When User Unauthenticated")]
    public async Task AuthorizationHandler_DoesNotSucceed_WhenUserUnauthenticated()
    {
        var handler = new KyrolusPermissionAuthorizationHandler(_resolver);
        var unauthPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var requirement = new KyrolusPermissionRequirement(["orders.read"]);
        var context = new AuthorizationHandlerContext([requirement], unauthPrincipal, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact(DisplayName = "Di Registration Registers Permissions")]
    public void DiRegistration_RegistersPermissions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusPermissions();

        var provider = services.BuildServiceProvider();

        provider.GetService<IKyrolusPermissionResolver>().ShouldNotBeNull();
        provider.GetServices<IAuthorizationHandler>().ShouldContain(h => h is KyrolusPermissionAuthorizationHandler);
    }

    [Fact(DisplayName = "Permission Handler Fails Closed When Permissions List Is Empty")]
    public async Task PermissionHandler_FailsClosed_WhenPermissionsListIsEmpty()
    {
        var handler = new KyrolusPermissionAuthorizationHandler(_resolver);
        var identity = new ClaimsIdentity([new Claim("permission", "orders.read")], "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Empty permissions requirement should NEVER succeed
        var emptyRequirement = new KyrolusPermissionRequirement([]);
        var context = new AuthorizationHandlerContext([emptyRequirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact(DisplayName = "Permission Handler Succeeds With Different Casing")]
    public async Task PermissionHandler_Succeeds_WithDifferentCasing()
    {
        var handler = new KyrolusPermissionAuthorizationHandler(_resolver);
        // User has lowercase permission
        var identity = new ClaimsIdentity([new Claim("permission", "users.read")], "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Requirement requires uppercase/titlecase permission
        var requirement = new KyrolusPermissionRequirement(["USERS.READ"]);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Theory(DisplayName = "Permission Handler Succeeds With Wildcard And Hierarchy Permissions")]
    [InlineData("*", "orders.create")]
    [InlineData("orders.*", "orders.create")]
    [InlineData("orders:*", "orders:delete")]
    [InlineData("users.*", "users.profile.read")]
    public async Task PermissionHandler_Succeeds_WithWildcardAndHierarchyPermissions(string userWildcard, string requiredPermission)
    {
        var handler = new KyrolusPermissionAuthorizationHandler(_resolver);
        var identity = new ClaimsIdentity([new Claim("permission", userWildcard)], "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var requirement = new KyrolusPermissionRequirement([requiredPermission]);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Theory(DisplayName = "Permission Handler Rejects Malformed Permissions With Consecutive Separators")]
    [InlineData("orders..create")]
    [InlineData("users::read")]
    public async Task PermissionHandler_RejectsMalformedPermissionsWithConsecutiveSeparators(string malformedPerm)
    {
        var handler = new KyrolusPermissionAuthorizationHandler(_resolver);
        var identity = new ClaimsIdentity([new Claim("permission", malformedPerm)], "TestAuth");
        var user = new ClaimsPrincipal(identity);

        var requirement = new KyrolusPermissionRequirement([malformedPerm]);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact(DisplayName = "Permission Requirement Sanitizes And Deduplicates Permissions")]
    public void PermissionRequirement_SanitizesAndDeduplicatesPermissions()
    {
        var raw = new[] { "orders.read", "  ", "ORDERS.READ", "  orders.write  " };
        var requirement = new KyrolusPermissionRequirement(raw);

        requirement.Permissions.Count.ShouldBe(2);
        requirement.Permissions.ShouldContain("orders.read");
        requirement.Permissions.ShouldContain("orders.write");
    }
}
