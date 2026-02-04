using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Auth;

public sealed record TestUser(string Subject, string UserName, string Password, string Email, string? TenantId);

public sealed class TestUserStore
{
    private readonly IReadOnlyList<TestUser> users =
    [
        new TestUser("user-1", "admin", "admin123", "admin@local.test", "tenant-alpha"),
        new TestUser("user-2", "cashier", "cashier123", "cashier@local.test", "tenant-beta")
    ];

    public TestUser? Validate(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return null;
        return users.FirstOrDefault(u =>
            string.Equals(u.UserName, username, StringComparison.OrdinalIgnoreCase)
            && string.Equals(u.Password, password, StringComparison.Ordinal));
    }

    public ClaimsPrincipal BuildPrincipal(TestUser user, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(Claims.Subject, user.Subject));
        identity.AddClaim(new Claim(Claims.Email, user.Email));
        identity.AddClaim(new Claim("tenant_id", user.TenantId ?? string.Empty));
        identity.AddClaim(new Claim(Claims.Scope, string.Join(" ", scopes)));

        return new ClaimsPrincipal(identity);
    }
}
