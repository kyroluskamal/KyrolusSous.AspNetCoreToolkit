using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.TokenRevocation;

/// <summary>
/// HTTP request pipeline middleware that validates authenticated bearer tokens against the <see cref="IKyrolusTokenBlacklist"/>.
/// Terminates revoked or blacklisted sessions immediately with HTTP 401 Unauthorized before subsequent middlewares or endpoints execute.
/// </summary>
public sealed class KyrolusTokenRevocationMiddleware(RequestDelegate next)
{
    private static readonly byte[] RevokedResponseBytes =
        """{"title":"Unauthorized","status":401,"detail":"The presented access token has been revoked or invalidated."}"""u8.ToArray();

    /// <summary>
    /// Invokes the middleware to validate token validity against <see cref="IKyrolusTokenBlacklist"/>.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity is { IsAuthenticated: true })
        {
            var blacklist = context.RequestServices.GetService<IKyrolusTokenBlacklist>();
            if (blacklist is not null)
            {
                var isValid = await KyrolusTokenRevocationValidator.IsValidAsync(
                    context.User,
                    blacklist,
                    context.RequestAborted);

                if (!isValid)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";
                    context.Response.Headers.Append("WWW-Authenticate", "Bearer error=\"invalid_token\", error_description=\"The access token has been revoked.\"");
                    await context.Response.Body.WriteAsync(RevokedResponseBytes, context.RequestAborted);
                    return;
                }
            }
        }

        await next(context);
    }
}
