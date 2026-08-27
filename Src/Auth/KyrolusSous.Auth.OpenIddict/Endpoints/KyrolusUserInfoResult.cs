using System.Security.Claims;
using System.Text.Json;
using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KyrolusSous.Auth.OpenIddict;

/// <summary>
/// Writes an OpenID Connect userinfo response, disclosing only what the granted scopes allow.
/// </summary>
/// <remarks>
/// Written with <see cref="Utf8JsonWriter"/> rather than a serializer: the payload is a
/// heterogeneous bag of strings, booleans and arrays, which a reflection-free serializer models
/// badly, and hand-writing it keeps the whole package trim- and AOT-safe.
/// </remarks>
internal sealed class KyrolusUserInfoResult(KyrolusAuthUser user, ClaimsPrincipal principal) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        await using var writer = new Utf8JsonWriter(httpContext.Response.BodyWriter);

        writer.WriteStartObject();

        // sub is the one claim userinfo must always return, whatever the scopes.
        writer.WriteString(Claims.Subject, user.Id);

        if (principal.HasScope(Scopes.Profile))
        {
            WriteIfPresent(writer, Claims.Name, user.DisplayName ?? user.UserName);
            WriteIfPresent(writer, Claims.PreferredUsername, user.UserName);
            WriteIfPresent(writer, Claims.Picture, GetUserClaim(KyrolusAuthConstants.Claims.Picture));
        }

        if (principal.HasScope(Scopes.Email) && !string.IsNullOrWhiteSpace(user.Email))
        {
            writer.WriteString(Claims.Email, user.Email);
            writer.WriteBoolean(Claims.EmailVerified, user.EmailConfirmed);
        }

        if (principal.HasScope(Scopes.Phone) && !string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            writer.WriteString(Claims.PhoneNumber, user.PhoneNumber);
            writer.WriteBoolean(Claims.PhoneNumberVerified, user.PhoneNumberConfirmed);
        }

        if (principal.HasScope(Scopes.Roles) && user.Roles.Count > 0)
        {
            writer.WriteStartArray(Claims.Role);
            foreach (var role in user.Roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    writer.WriteStringValue(role);
                }
            }

            writer.WriteEndArray();
        }

        WriteIfPresent(writer, KyrolusAuthConstants.Claims.TenantId, user.TenantId);

        foreach (var claim in user.Claims)
        {
            // Picture is already emitted under the profile scope; re-emitting it here would
            // disclose it to a client that only asked for openid.
            if (string.Equals(claim.Key, KyrolusAuthConstants.Claims.Picture, StringComparison.Ordinal))
            {
                continue;
            }

            WriteIfPresent(writer, claim.Key, claim.Value);
        }

        writer.WriteEndObject();
        await writer.FlushAsync(httpContext.RequestAborted).ConfigureAwait(false);
    }

    private string? GetUserClaim(string claimType)
        => user.Claims.TryGetValue(claimType, out var value) ? value : null;

    private static void WriteIfPresent(Utf8JsonWriter writer, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(name, value);
        }
    }
}
