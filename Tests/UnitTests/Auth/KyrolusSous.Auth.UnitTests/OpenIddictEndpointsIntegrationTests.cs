using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.OpenIddict;
using KyrolusSous.Auth.OpenIddict.Config;
using KyrolusSous.Auth.OpenIddict.Options;
using KyrolusSous.Auth.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Shouldly;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KyrolusSous.Auth.UnitTests;

/// <summary>
/// Drives the mapped endpoints through a real HTTP pipeline. Unit tests can prove the options are
/// wired correctly; only an end-to-end run proves a token actually comes out the other side.
/// </summary>
public sealed class OpenIddictEndpointsIntegrationTests : IAsyncLifetime
{
    private const string ClientId = "test-client";
    private const string UserName = "ada";
    private const string Password = "s3cret-passphrase";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // The application owns its storage. Entity Framework here is a choice this test makes,
        // not something the Kyrolus auth packages reference.
        //
        // The name is computed once, outside the callback: AddDbContext builds its options per
        // scope, so a Guid generated inside would hand every scope its own empty database and the
        // seeded client would vanish between seeding and the first request.
        var databaseName = $"auth-{Guid.NewGuid():N}";
        builder.Services.AddDbContext<TestAuthDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.UseOpenIddict();
        });

        builder.Services.AddOpenIddict()
            .AddCore(core => core.UseEntityFrameworkCore().UseDbContext<TestAuthDbContext>());

        builder.Services.AddKyrolusOpenIddictAuthServer(options =>
        {
            options.UseEphemeralKeys = true;
            options.AllowPasswordFlow = true;
            options.AllowRefreshTokenFlow = true;
            options.AllowAuthorizationCodeFlow = false;
            options.DisableTransportSecurityRequirement = true;
            options.EnrichErrorResponses = true;
            options.AccessTokenLifetime = TimeSpan.FromMinutes(5);
            options.RefreshTokenLifetime = TimeSpan.FromHours(1);
        });

        var hasher = new KyrolusPbkdf2PasswordHasher(
            global::Microsoft.Extensions.Options.Options.Create(new KyrolusAuthOptions { Pbkdf2Iterations = 10_000 }));

        builder.Services.AddKyrolusAuthCore(o => o.Pbkdf2Iterations = 10_000);
        builder.Services.AddKyrolusInMemoryAuthUserStore(store => store.Add(new KyrolusAuthUser
        {
            Id = "user-1",
            UserName = UserName,
            Email = "ada@contoso.com",
            EmailConfirmed = true,
            DisplayName = "Ada Lovelace",
            PasswordHash = hasher.Hash(Password),
            Roles = { "Admin" },
            TenantId = "contoso",
        }));

        _app = builder.Build();

        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapKyrolusOpenIddictEndpoints();

        await _app.StartAsync();
        await SeedClientAsync();

        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _app.DisposeAsync();
    }

    private async Task SeedClientAsync()
    {
        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestAuthDbContext>().Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = ClientTypes.Public,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
            },
        });
    }

    private Task<HttpResponseMessage> PostTokenAsync(params (string Key, string Value)[] fields)
        => _client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))));

    /// <summary>
    /// Asserts the status and, when it does not match, puts the OAuth error body in the failure
    /// message - a bare "expected OK, got BadRequest" says nothing about which of the dozen
    /// protocol preconditions was not met.
    /// </summary>
    private static async Task ShouldHaveStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, body);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact(DisplayName = "The password grant issues a token")]
    public async Task The_password_grant_issues_a_token()
    {
        var response = await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", Password),
            ("scope", "openid email profile roles offline_access"));

        await ShouldHaveStatusAsync(response, HttpStatusCode.OK);

        var payload = await ReadJsonAsync(response);
        payload.GetProperty("access_token").GetString().ShouldNotBeNullOrWhiteSpace();
        payload.GetProperty("refresh_token").GetString().ShouldNotBeNullOrWhiteSpace();
        payload.GetProperty("token_type").GetString().ShouldBe("Bearer");
        payload.GetProperty("expires_in").GetInt64().ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "A wrong password is refused without leaking which half was wrong")]
    public async Task A_wrong_password_is_refused_without_leaking_which_half_was_wrong()
    {
        var wrongPassword = await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", "not-the-password"),
            ("scope", "openid"));

        var unknownUser = await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", "grace"),
            ("password", Password),
            ("scope", "openid"));

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var first = await ReadJsonAsync(wrongPassword);
        var second = await ReadJsonAsync(unknownUser);

        first.GetProperty("error").GetString().ShouldBe(Errors.InvalidGrant);
        first.GetProperty("error_description").GetString()
            .ShouldBe(second.GetProperty("error_description").GetString());
    }

    [Fact(DisplayName = "Error responses keep the standard oauth fields and add the kyrolus code")]
    public async Task Error_responses_keep_the_standard_oauth_fields_and_add_the_kyrolus_code()
    {
        var response = await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", "wrong"),
            ("scope", "openid"));

        var payload = await ReadJsonAsync(response);

        // A conforming client reads these two and keeps working.
        payload.GetProperty("error").GetString().ShouldBe(Errors.InvalidGrant);
        payload.TryGetProperty("error_description", out _).ShouldBeTrue();

        // A Kyrolus client gets the house error code alongside them.
        payload.GetProperty("error_code").GetString().ShouldBe("unauthorized");
    }

    [Fact(DisplayName = "Missing credentials produce field level errors")]
    public async Task Missing_credentials_produce_field_level_errors()
    {
        var response = await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("scope", "openid"));

        var payload = await ReadJsonAsync(response);

        payload.GetProperty("error").GetString().ShouldBe(Errors.InvalidRequest);

        var fields = payload.GetProperty("errors")
            .EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToList();

        fields.ShouldContain("username");
        fields.ShouldContain("password");
    }

    [Fact(DisplayName = "An unsupported grant type is refused")]
    public async Task An_unsupported_grant_type_is_refused()
    {
        var response = await PostTokenAsync(
            ("grant_type", "authorization_code"),
            ("client_id", ClientId),
            ("code", "whatever"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("error").GetString()
            .ShouldBe(Errors.UnsupportedGrantType);
    }

    [Fact(DisplayName = "A refresh token can be redeemed")]
    public async Task A_refresh_token_can_be_redeemed()
    {
        var first = await ReadJsonAsync(await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", Password),
            ("scope", "openid email offline_access")));

        var refreshed = await PostTokenAsync(
            ("grant_type", "refresh_token"),
            ("client_id", ClientId),
            ("refresh_token", first.GetProperty("refresh_token").GetString()!));

        await ShouldHaveStatusAsync(refreshed, HttpStatusCode.OK);
        (await ReadJsonAsync(refreshed)).GetProperty("access_token").GetString()
            .ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "A refresh can narrow the granted scopes")]
    public async Task A_refresh_can_narrow_the_granted_scopes()
    {
        var first = await ReadJsonAsync(await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", Password),
            ("scope", "openid email profile offline_access")));

        var refreshed = await PostTokenAsync(
            ("grant_type", "refresh_token"),
            ("client_id", ClientId),
            ("refresh_token", first.GetProperty("refresh_token").GetString()!),
            ("scope", "openid offline_access"));

        await ShouldHaveStatusAsync(refreshed, HttpStatusCode.OK);

        var token = (await ReadJsonAsync(refreshed)).GetProperty("access_token").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // The narrowed token must not still open the email claim.
        var payload = await ReadJsonAsync(await _client.SendAsync(request));
        payload.GetProperty("sub").GetString().ShouldBe("user-1");
        payload.TryGetProperty("email", out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "Disabling an account stops the next refresh")]
    public async Task Disabling_an_account_stops_the_next_refresh()
    {
        var first = await ReadJsonAsync(await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", Password),
            ("scope", "openid offline_access")));

        // The endpoint re-reads the user on every redemption, so a revocation takes effect at the
        // next refresh rather than whenever the last long-lived token happens to expire.
        _app.Services.GetRequiredService<KyrolusInMemoryAuthUserStore>()
            .Users.Single().IsActive = false;

        var refreshed = await PostTokenAsync(
            ("grant_type", "refresh_token"),
            ("client_id", ClientId),
            ("refresh_token", first.GetProperty("refresh_token").GetString()!));

        refreshed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(refreshed)).GetProperty("error_description").GetString()
            .ShouldNotBeNull()
            .ShouldContain("disabled");
    }

    [Fact(DisplayName = "Userinfo returns the claims the granted scopes allow")]
    public async Task Userinfo_returns_the_claims_the_granted_scopes_allow()
    {
        var token = (await ReadJsonAsync(await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", Password),
            ("scope", "openid email profile roles")))).GetProperty("access_token").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        await ShouldHaveStatusAsync(response, HttpStatusCode.OK);

        var payload = await ReadJsonAsync(response);
        payload.GetProperty("sub").GetString().ShouldBe("user-1");
        payload.GetProperty("email").GetString().ShouldBe("ada@contoso.com");
        payload.GetProperty("email_verified").GetBoolean().ShouldBeTrue();
        payload.GetProperty("name").GetString().ShouldBe("Ada Lovelace");
        payload.GetProperty("role").EnumerateArray().Select(e => e.GetString()).ShouldContain("Admin");
        payload.GetProperty("tenant_id").GetString().ShouldBe("contoso");
    }

    [Fact(DisplayName = "Userinfo withholds the email when its scope was not granted")]
    public async Task Userinfo_withholds_the_email_when_its_scope_was_not_granted()
    {
        var token = (await ReadJsonAsync(await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", Password),
            ("scope", "openid")))).GetProperty("access_token").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = await ReadJsonAsync(await _client.SendAsync(request));

        payload.GetProperty("sub").GetString().ShouldBe("user-1");
        payload.TryGetProperty("email", out _).ShouldBeFalse();
        payload.TryGetProperty("name", out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "Userinfo refuses an unauthenticated request")]
    public async Task Userinfo_refuses_an_unauthenticated_request()
    {
        var response = await _client.GetAsync("/connect/userinfo");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Token endpoint refuses password grant when Allow Password Flow is disabled")]
    public async Task Token_endpoint_refuses_password_grant_when_AllowPasswordFlow_is_disabled()
    {
        var options = _app.Services.GetRequiredService<KyrolusOpenIddictOptions>();
        options.AllowPasswordFlow = false;

        try
        {
            var response = await PostTokenAsync(
                ("grant_type", "password"),
                ("client_id", ClientId),
                ("username", UserName),
                ("password", Password),
                ("scope", "openid"));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var payload = await ReadJsonAsync(response);
            payload.GetProperty("error").GetString().ShouldBe("unsupported_grant_type");
        }
        finally
        {
            options.AllowPasswordFlow = true;
        }
    }

    [Fact(DisplayName = "A refresh token is refused when user is locked out")]
    public async Task A_refresh_token_is_refused_when_user_is_locked_out()
    {
        var first = await ReadJsonAsync(await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", Password),
            ("scope", "openid email offline_access")));

        var userStore = _app.Services.GetRequiredService<IKyrolusAuthUserStore>();
        var user = await userStore.FindByIdAsync("user-1");
        user!.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);

        try
        {
            var refreshed = await PostTokenAsync(
                ("grant_type", "refresh_token"),
                ("client_id", ClientId),
                ("refresh_token", first.GetProperty("refresh_token").GetString()!));

            refreshed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var payload = await ReadJsonAsync(refreshed);
            payload.GetProperty("error").GetString().ShouldBe("invalid_grant");
            payload.GetProperty("error_description").GetString()!.ShouldContain("locked out");
        }
        finally
        {
            user.LockoutEnd = null;
        }
    }

    [Fact(DisplayName = "Userinfo refuses when user is deactivated")]
    public async Task Userinfo_refuses_when_user_is_deactivated()
    {
        var tokenResponse = await ReadJsonAsync(await PostTokenAsync(
            ("grant_type", "password"),
            ("client_id", ClientId),
            ("username", UserName),
            ("password", Password),
            ("scope", "openid email")));

        var accessToken = tokenResponse.GetProperty("access_token").GetString()!;

        var userStore = _app.Services.GetRequiredService<IKyrolusAuthUserStore>();
        var user = await userStore.FindByIdAsync("user-1");
        user!.IsActive = false;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var res = await _client.SendAsync(req);
            res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        finally
        {
            user.IsActive = true;
        }
    }

    private sealed class TestAuthDbContext(DbContextOptions<TestAuthDbContext> options) : DbContext(options);
}
