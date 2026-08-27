using System.Security.Claims;
using KyrolusSous.Auth.Abstractions;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Auth.Runtime;

/// <summary>
/// The default external-login handler: resolves the local account behind an external identity,
/// optionally linking it to an existing account by verified email or provisioning a new one, and
/// attaches the local claims to the principal.
/// </summary>
public sealed class KyrolusExternalLoginHandler(
    IKyrolusAuthUserStore userStore,
    ILogger<KyrolusExternalLoginHandler> logger) : IKyrolusExternalLoginHandler
{
    /// <inheritdoc />
    public async ValueTask<KyrolusExternalLoginResult> HandleAsync(
        KyrolusExternalLoginInfo info,
        KyrolusExternalLoginOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(info.ProviderKey))
        {
            logger.LogWarning(
                "{Provider} returned an identity with no stable subject identifier; refusing the sign-in.",
                info.ProviderName);

            return KyrolusExternalLoginResult.Fail(
                KyrolusAuthConstants.Errors.ExternalLoginFailed,
                $"{info.ProviderName} did not return a user identifier.");
        }

        var user = await userStore
            .FindByExternalLoginAsync(info.ProviderName, info.ProviderKey, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            user = await TryLinkByEmailAsync(info, options, cancellationToken).ConfigureAwait(false);
        }

        if (user is null && options.AutoCreateUser)
        {
            user = await ProvisionAsync(info, options, cancellationToken).ConfigureAwait(false);
        }

        if (user is null)
        {
            return KyrolusExternalLoginResult.Fail(
                KyrolusAuthConstants.Errors.UserNotFound,
                $"No local account is linked to this {info.ProviderName} identity.");
        }

        if (!user.IsActive)
        {
            return KyrolusExternalLoginResult.Fail(
                KyrolusAuthConstants.Errors.UserInactive,
                "The account is disabled.");
        }

        return KyrolusExternalLoginResult.Success(BuildLocalClaims(user));
    }

    private async ValueTask<KyrolusAuthUser?> TryLinkByEmailAsync(
        KyrolusExternalLoginInfo info,
        KyrolusExternalLoginOptions options,
        CancellationToken cancellationToken)
    {
        // Both conditions matter. Linking on an unverified email lets anyone who can create an
        // account at a lax provider, using someone else's address, take over the local account.
        if (!options.LinkToExistingUserByEmail || !info.EmailVerified || string.IsNullOrWhiteSpace(info.Email))
        {
            return null;
        }

        var existing = await userStore.FindByEmailAsync(info.Email, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        await userStore
            .AddExternalLoginAsync(existing.Id, info.ProviderName, info.ProviderKey, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Linked {Provider} identity to existing account {UserId} by verified email.",
            info.ProviderName,
            existing.Id);

        return existing;
    }

    private async ValueTask<KyrolusAuthUser?> ProvisionAsync(
        KyrolusExternalLoginInfo info,
        KyrolusExternalLoginOptions options,
        CancellationToken cancellationToken)
    {
        var safeEmail = !string.IsNullOrWhiteSpace(info.Email) ? info.Email.Trim() : null;
        var user = new KyrolusAuthUser
        {
            UserName = safeEmail ?? $"{info.ProviderName.ToLowerInvariant()}:{info.ProviderKey}",
            Email = safeEmail,
            EmailConfirmed = info.EmailVerified,
            DisplayName = info.DisplayName,
            IsActive = true,
            TenantId = options.DefaultTenantId
        };

        if (!string.IsNullOrWhiteSpace(options.DefaultRole))
        {
            user.Roles.Add(options.DefaultRole);
        }

        if (!string.IsNullOrWhiteSpace(info.PictureUrl))
        {
            user.Claims[KyrolusAuthConstants.Claims.Picture] = info.PictureUrl;
        }

        var created = await userStore.CreateAsync(user, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(created.Id))
        {
            logger.LogError(
                "The user store returned a user with no Id after provisioning a {Provider} identity. " +
                "IKyrolusAuthUserStore.CreateAsync must return the stored record with its identifier populated.",
                info.ProviderName);

            return null;
        }

        await userStore
            .AddExternalLoginAsync(created.Id, info.ProviderName, info.ProviderKey, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Provisioned account {UserId} from a {Provider} identity.",
            created.Id,
            info.ProviderName);

        return created;
    }

    private static List<Claim> BuildLocalClaims(KyrolusAuthUser user)
    {
        // NameIdentifier is left alone: the provider already put its own subject there, and
        // overwriting it would lose the link back to the external identity.
        var claims = new List<Claim>(2 + user.Roles.Count)
        {
            new(KyrolusAuthConstants.Claims.Sub, user.Id),
        };

        foreach (var role in user.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(KyrolusAuthConstants.Claims.Role, role));
            }
        }

        if (!string.IsNullOrWhiteSpace(user.TenantId))
        {
            claims.Add(new Claim(KyrolusAuthConstants.Claims.TenantId, user.TenantId));
        }

        return claims;
    }
}
