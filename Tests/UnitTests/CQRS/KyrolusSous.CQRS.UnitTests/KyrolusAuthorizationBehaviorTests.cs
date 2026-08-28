using System.Security.Claims;
using KyrolusSous.CQRS.Abstractions.Attributes;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusAuthorizationBehaviorTests
{
    [KyrolusAuthorize(Roles = "Admin,Manager")]
    public sealed record AdminCommand(string Action) : IKyrolusCommand<string>;

    [KyrolusAuthorize(Permissions = "orders.create")]
    public sealed record CreateOrderCommand(int Amount) : IKyrolusCommand<int>;

    public sealed record CustomAuthorizedRequest(string Data, IReadOnlyCollection<string>? RequiredRoles, IReadOnlyCollection<string>? RequiredPermissions)
        : IKyrolusRequest<string>, IAuthorizedRequest;

    public sealed record PublicQuery(string Query) : IKyrolusQuery<string>;

    [Fact(DisplayName = "Public request should proceed without authentication")]
    public async Task Public_request_should_proceed_without_authentication()
    {
        var behavior = new KyrolusAuthorizationBehavior<PublicQuery, string>(new KyrolusDefaultCurrentUserContext());
        var response = await behavior.Handle(
            new PublicQuery("search"),
            ct => Task.FromResult("results"),
            CancellationToken.None);

        response.ShouldBe("results");
    }

    [Fact(DisplayName = "Unauthenticated user on protected command should throw security exception")]
    public async Task Unauthenticated_user_on_protected_command_should_throw_security_exception()
    {
        var unauthenticatedContext = new KyrolusDefaultCurrentUserContext(new ClaimsPrincipal(new ClaimsIdentity())); // not authenticated
        var behavior = new KyrolusAuthorizationBehavior<AdminCommand, string>(unauthenticatedContext);

        var ex = await Should.ThrowAsync<KyrolusSecurityException>(() =>
            behavior.Handle(new AdminCommand("delete"), ct => Task.FromResult("ok"), CancellationToken.None));

        ex.Message.ShouldContain("not authenticated");
    }

    [Fact(DisplayName = "User with required role should succeed")]
    public async Task User_with_required_role_should_succeed()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Name, "Alice"),
            new Claim(ClaimTypes.Role, "Admin")
        ], "TestAuth");

        var context = new KyrolusDefaultCurrentUserContext(new ClaimsPrincipal(identity));
        var behavior = new KyrolusAuthorizationBehavior<AdminCommand, string>(context);

        var response = await behavior.Handle(
            new AdminCommand("delete"),
            ct => Task.FromResult("deleted"),
            CancellationToken.None);

        response.ShouldBe("deleted");
    }

    [Fact(DisplayName = "User without required role should throw security exception")]
    public async Task User_without_required_role_should_throw_security_exception()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-2"),
            new Claim(ClaimTypes.Role, "Guest")
        ], "TestAuth");

        var context = new KyrolusDefaultCurrentUserContext(new ClaimsPrincipal(identity));
        var behavior = new KyrolusAuthorizationBehavior<AdminCommand, string>(context);

        var ex = await Should.ThrowAsync<KyrolusSecurityException>(() =>
            behavior.Handle(new AdminCommand("delete"), ct => Task.FromResult("ok"), CancellationToken.None));

        ex.RequiredClaim.ShouldBe("Admin,Manager");
    }

    [Fact(DisplayName = "User with permission should succeed")]
    public async Task User_with_permission_should_succeed()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-3"),
            new Claim("permission", "orders.create")
        ], "TestAuth");

        var context = new KyrolusDefaultCurrentUserContext(new ClaimsPrincipal(identity));
        var behavior = new KyrolusAuthorizationBehavior<CreateOrderCommand, int>(context);

        var response = await behavior.Handle(
            new CreateOrderCommand(100),
            ct => Task.FromResult(100),
            CancellationToken.None);

        response.ShouldBe(100);
    }

    [Fact(DisplayName = "Programmatic authorization request should validate properly")]
    public async Task Programmatic_authorization_request_should_validate_properly()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-4"),
            new Claim(ClaimTypes.Role, "Supervisor")
        ], "TestAuth");

        var context = new KyrolusDefaultCurrentUserContext(new ClaimsPrincipal(identity));
        var behavior = new KyrolusAuthorizationBehavior<CustomAuthorizedRequest, string>(context);

        // Success when role matches
        var response = await behavior.Handle(
            new CustomAuthorizedRequest("data", ["Supervisor"], null),
            ct => Task.FromResult("ok"),
            CancellationToken.None);
        response.ShouldBe("ok");

        // Fails when required permission missing
        await Should.ThrowAsync<KyrolusSecurityException>(() =>
            behavior.Handle(
                new CustomAuthorizedRequest("data", ["Supervisor"], ["missing.perm"]),
                ct => Task.FromResult("ok"),
                CancellationToken.None));
    }
}
