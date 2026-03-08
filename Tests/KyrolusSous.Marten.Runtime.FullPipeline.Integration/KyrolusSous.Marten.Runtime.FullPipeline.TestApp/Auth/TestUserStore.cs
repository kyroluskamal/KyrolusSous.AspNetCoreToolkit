using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Auth;

public sealed record TestUser(string Subject, string UserName, string Password, string Email, string? TenantId);

public sealed class TestUserStore
{
    private readonly IReadOnlyList<TestUser> users =
    [
        new TestUser("user-1", "admin", "admin123", "admin@local.test", "tenant-alpha"),
        new TestUser("user-2", "cashier", "cashier123", "cashier@local.test", "tenant-beta"),
        new TestUser("user-3", "no-tenant", "notenant123", "notenant@local.test", null)
    ];

    public TestUser? Validate(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return null;
        return users.FirstOrDefault(u =>
            string.Equals(u.UserName, username, StringComparison.OrdinalIgnoreCase)
            && string.Equals(u.Password, password, StringComparison.Ordinal));
    }

    public IReadOnlyList<Claim> BuildClaims(TestUser user, IEnumerable<string> scopes)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Subject),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("scope", string.Join(" ", scopes))
        };

        if (!string.IsNullOrWhiteSpace(user.TenantId))
        {
            claims.Add(new Claim("tenant_id", user.TenantId));
        }

        return claims;
    }
}
