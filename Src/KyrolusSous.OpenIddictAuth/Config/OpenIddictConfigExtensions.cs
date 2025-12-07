using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Microsoft.AspNetCore.Http;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;
using KyrolusSous.ExceptionHandling.Handlers;
using OpenIddict.Abstractions;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using KyrolusSous.OpenIddictAuth.Data;
namespace KyrolusSous.OpenIddictAuth.Config;

public static class OpenIddictConfigExtensions
{
    public static void AddOpenIddictConfigForAuthServer(this IServiceCollection services,
    IConfiguration configuration, string migrationsAssemblyName, string tokenEndpointName = "/login")
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 10,
                    maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null
                );
                sql.MigrationsAssembly(migrationsAssemblyName);
            });
            options.UseOpenIddict<OpenIddictEntityFrameworkCoreApplication<int>,
                             OpenIddictEntityFrameworkCoreAuthorization<int>, OpenIddictEntityFrameworkCoreScope<int>, OpenIddictEntityFrameworkCoreToken<int>, int>();
        });
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                        .UseDbContext<ApplicationDbContext>().ReplaceDefaultEntities<int>();
            })
            .AddServer(options =>
            {
                options.SetTokenEndpointUris(tokenEndpointName);
                options.AllowPasswordFlow().AllowRefreshTokenFlow();
                options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess);
                var signingCertificate = X509CertificateLoader.LoadPkcs12(
                    File.ReadAllBytes("/https/aspnetapp.pfx"),
                    "codingbible",
                    X509KeyStorageFlags.MachineKeySet
                );
                options.AddSigningCertificate(signingCertificate).AddEncryptionCertificate(signingCertificate);
                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough();
                options.SetIntrospectionEndpointUris("/introspect");

                // Set the access token lifetime (optional)
                options.SetAccessTokenLifetime(TimeSpan.FromDays(30));
                // options.SetRefreshTokenLifetime(TimeSpan.FromSeconds(60));
                // إضافة Event Handler لتخصيص الاستجابة
                options.AddEventHandler<ApplyTokenResponseContext>(builder =>
                    {
                        builder.UseSingletonHandler<MyApplyTokenResponseHandler>();
                    });
                options.AddEventHandler<ApplyIntrospectionResponseContext>(builder =>
                    {
                        builder.UseSingletonHandler<AccessTokenCheckHandler>();
                    });

            });

        services.AddAuthentication(options =>
            {
                // Set the default schemes for Identity
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddCookie(opt =>
            {
                opt.Cookie.HttpOnly = true;
                opt.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Set to CookieSecurePolicy.None in development
                opt.Cookie.SameSite = SameSiteMode.Strict;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = "https://auth:8081";
                options.RequireHttpsMetadata = false; // Set to true in production
            });
        // services.AddAuthentication(options =>
        // {
        //     options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        //     options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        // }).AddCookie(IdentityConstants.ApplicationScheme)
        // .AddJwtBearer(options =>
        // {
        //     options.Authority = "https://auth:8081";
        //     options.RequireHttpsMetadata = false; // Set to true in production
        // });
        services.AddAuthorization();
    }

    public static void AddOpenIddictConfigForApiServer(this IServiceCollection services, string issure, string clientId, string clientSecret)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = issure;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issure,
                    ValidateAudience = false,
                    // ValidAudience = "your-audience",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = false,
                };
                options.RequireHttpsMetadata = false; // Set to true in production
            });
        services.AddOpenIddict()
            .AddValidation(options =>
            {
                options.SetIssuer(issure); // Authentication server address
                options.UseSystemNetHttp();
                options.UseAspNetCore();
                options.UseIntrospection()   //we have to use interospection because the token is Encrypted
                    .SetClientId(clientId)
                    .SetClientSecret(clientSecret);
            });
    }
}
public class MyApplyTokenResponseHandler : IOpenIddictServerHandler<ApplyTokenResponseContext>
{
    public ValueTask HandleAsync(ApplyTokenResponseContext context)
    {
        var response = context.Response;
        if (string.Equals(response.Error, Errors.InvalidRequest, StringComparison.Ordinal) &&
            response.ErrorDescription != null && response.ErrorDescription.Contains("'username' and/or 'password' parameters are missing") && context.Request?.GrantType == GrantTypes.Password)
        {
            throw new FluentValidation.ValidationException("The username and password fields are required.");
        }

        return default;
    }
}
public class AccessTokenCheckHandler : IOpenIddictServerHandler<ApplyIntrospectionResponseContext>
{
    public ValueTask HandleAsync(ApplyIntrospectionResponseContext context)
    {
        var response = context.Response;

        if (response["active"] is OpenIddictParameter activeParameter &&
            (bool?)activeParameter is bool isActive && !isActive)
        {
            throw new UnauthorizedException("The access token is no longer valid or expired.");
        }
        return default;
    }
}
