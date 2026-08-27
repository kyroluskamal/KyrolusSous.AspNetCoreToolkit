using System.Security.Claims;
using KyrolusSous.Auth.OpenIddict;
using OpenIddict.Abstractions;
using Shouldly;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KyrolusSous.Auth.UnitTests;

public sealed class KyrolusClaimDestinationsTests
{
    private static ClaimsPrincipal PrincipalWithScopes(params string[] scopes)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        principal.SetScopes(scopes);
        return principal;
    }

    [Theory]
    [InlineData(Claims.Email, Scopes.Email)]
    [InlineData(Claims.EmailVerified, Scopes.Email)]
    [InlineData(Claims.Name, Scopes.Profile)]
    [InlineData(Claims.GivenName, Scopes.Profile)]
    [InlineData(Claims.Picture, Scopes.Profile)]
    [InlineData(Claims.PhoneNumber, Scopes.Phone)]
    [InlineData(Claims.Role, Scopes.Roles)]
    public void A_scoped_claim_reaches_the_identity_token_only_with_its_scope(string claimType, string scope)
    {
        var claim = new Claim(claimType, "value");

        var without = KyrolusClaimDestinations
            .GetDestinations(claim, PrincipalWithScopes(Scopes.OpenId))
            .ToList();
        var with = KyrolusClaimDestinations
            .GetDestinations(claim, PrincipalWithScopes(Scopes.OpenId, scope))
            .ToList();

        without.ShouldBe([Destinations.AccessToken]);
        with.ShouldBe([Destinations.AccessToken, Destinations.IdentityToken]);
    }

    [Fact]
    public void An_application_claim_goes_to_the_access_token_only()
    {
        var destinations = KyrolusClaimDestinations
            .GetDestinations(new Claim("tenant_id", "contoso"), PrincipalWithScopes(Scopes.Profile))
            .ToList();

        // The identity token describes who the user is; it is not an authorization document.
        destinations.ShouldBe([Destinations.AccessToken]);
    }

    [Theory]
    [InlineData("AspNet.Identity.SecurityStamp")]
    [InlineData("security_stamp")]
    [InlineData("SecurityStamp")]
    [InlineData("password_hash")]
    [InlineData("PasswordHash")]
    [InlineData("concurrency_stamp")]
    [InlineData("ConcurrencyStamp")]
    public void Sensitive_claims_never_leave_the_server(string claimType)
    {
        var destinations = KyrolusClaimDestinations
            .GetDestinations(new Claim(claimType, "sensitive_val"), PrincipalWithScopes(Scopes.Profile, Scopes.Email))
            .ToList();

        destinations.ShouldBeEmpty();
    }

    [Fact]
    public void SetKyrolusDestinations_stamps_every_claim()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim(Claims.Subject, "user-1"));
        identity.AddClaim(new Claim(Claims.Email, "ada@contoso.com"));
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(Scopes.Email);

        principal.SetKyrolusDestinations();

        principal.FindFirst(Claims.Subject)!.GetDestinations()
            .ShouldBe([Destinations.AccessToken]);
        principal.FindFirst(Claims.Email)!.GetDestinations()
            .ShouldBe([Destinations.AccessToken, Destinations.IdentityToken]);
    }

    [Fact]
    public void SetKyrolusDestinations_honours_a_custom_resolver()
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("internal_only", "x"));
        var principal = new ClaimsPrincipal(identity);

        principal.SetKyrolusDestinations((claim, _) =>
            claim.Type == "internal_only" ? [] : [Destinations.AccessToken]);

        principal.FindFirst("internal_only")!.GetDestinations().ShouldBeEmpty();
    }
}
