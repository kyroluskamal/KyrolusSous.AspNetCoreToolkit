using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Auth;

public sealed class PasswordGrantHandler(TestUserStore userStore) : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly TestUserStore userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));

    public ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        if (!string.Equals(context.Request.GrantType, GrantTypes.Password, StringComparison.Ordinal))
        {
            return default;
        }

        var user = userStore.Validate(context.Request.Username, context.Request.Password);
        if (user is null)
        {
            context.Reject(
                error: Errors.InvalidGrant,
                description: "Invalid credentials.");
            return default;
        }

        context.Principal = userStore.BuildPrincipal(user, new[] { "api" });
        return default;
    }
}
