using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace KyrolusSous.AntiforgeryToken.Middleware;

public class AntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery, string cookieName = "XSRF-TOKEN")
{
    private readonly RequestDelegate _next = next;
    private readonly IAntiforgery _antiforgery = antiforgery;

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip CSRF validation for GET requests
        if (context.Request.Method == HttpMethods.Get)
        {
            await _next(context);
            return;
        }
        _antiforgery.GetAndStoreTokens(context);

        // Check if the CSRF token cookie exists
        var existingToken = context.Request.Cookies[cookieName];
        if (string.IsNullOrEmpty(existingToken))
        {
            // Generate and set a new CSRF token cookie
            var tokens = _antiforgery.GetTokens(context);
            context.Response.Cookies.Append(cookieName, tokens.RequestToken,
                new CookieOptions()
                {
                    HttpOnly = false,
                    Secure = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Domain = "localhost",
                    MaxAge = TimeSpan.FromDays(10)
                });
            try
            {
                // Validate the anti-forgery token
                await _antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                // If the token is invalid or expired, return a 400 Bad Request
                context.Response.Cookies.Append(
                    cookieName, // Use the configured cookie name
                    tokens.RequestToken!);
                return;
            }

            // Call the next middleware in the pipeline
        }
        await _next(context);
    }
}