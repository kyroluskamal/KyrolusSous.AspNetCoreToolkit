using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KyrolusSous.Swagger;

public static class SwaggerServiceExtension
{
    private static SwaggerServiceOptions? _swaggerOptions;

    public static void AddSwaggerService(this IServiceCollection services, Action<SwaggerServiceOptions>? configureOptions = null)
    {
        var options = new SwaggerServiceOptions();
        configureOptions?.Invoke(options);
        _swaggerOptions = options;

        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(c =>
        {
            foreach (var versionInfo in options.ApiVersions) AddSwaggerVersion(c, versionInfo);
            if (options.EnableXmlComments) EnableXmlComments(c, options);
            if (options.EnableJwtBearerAuth) EnableJwtBearerAuth(c, options);
            if (options.EnableOAuth2Auth) EnableOAuth2Auth(c, options);
            if (options.EnableAnnotations) c.EnableAnnotations();
            if (options.EnableNullableReferenceTypesSupport) c.SupportNonNullableReferenceTypes();

        });
    }

    public static void UseConfiguredSwaggerUI(this WebApplication app, Action<SwaggerUIOptions>? configureOptions = null)
    {
        var options = _swaggerOptions ?? new SwaggerServiceOptions();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(uiOptions =>
            {
                ConfigureSwaggerEndpoints(uiOptions, options);
                ApplyGeneralUiOptions(uiOptions, options);
                uiOptions.SupportedSubmitMethods([.. options.UiSupportedSubmitMethods.Select(s => Enum.Parse<SubmitMethod>(s, true))]);
                configureOptions?.Invoke(uiOptions);
            });
        }
    }
    private static void AddSwaggerVersion(SwaggerGenOptions c, ApiVersionInfo versionInfo)
    {
        var ContactUrl = (versionInfo.ContactUrl != null) ? new Uri(versionInfo.ContactUrl) : null;
        var LicenseUrl = (versionInfo.LicenseUrl != null) ? new Uri(versionInfo.LicenseUrl) : null;
        c.SwaggerDoc(versionInfo.Version, new OpenApiInfo
        {
            Title = versionInfo.Title,
            Version = versionInfo.Version,
            Description = versionInfo.Description,
            TermsOfService = versionInfo.TermsOfServiceUrl != null ? new Uri(versionInfo.TermsOfServiceUrl) : null,

            Contact = (versionInfo.ContactName != null && versionInfo.ContactEmail != null) ? new OpenApiContact
            {
                Name = versionInfo.ContactName,
                Email = versionInfo.ContactEmail,
                Url = ContactUrl
            } : null,
            License = (versionInfo.LicenseName != null) ? new OpenApiLicense
            {
                Name = versionInfo.LicenseName,
                Url = LicenseUrl
            } : null
        });
    }
    private static void EnableXmlComments(SwaggerGenOptions c, SwaggerServiceOptions options)
    {
        var assembliesToProcess = options.XmlCommentAssemblies.Count != 0 ? options.XmlCommentAssemblies : [Assembly.GetEntryAssembly()!];
        foreach (var assembly in assembliesToProcess)
        {
            var xmlFile = $"{assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        }
    }
    private static void EnableJwtBearerAuth(SwaggerGenOptions c, SwaggerServiceOptions options)
    {
        c.AddSecurityDefinition(options.JwtBearerScheme, new OpenApiSecurityScheme
        {
            Description = options.JwtBearerDescription,
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = options.JwtBearerScheme
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = options.JwtBearerScheme
                            }
                        },
                        Array.Empty<string>()
                    }
                });
    }

    private static void EnableOAuth2Auth(SwaggerGenOptions c, SwaggerServiceOptions options)
    {
        var flows = options.OAuth2Flow switch
        {
            "authorizationCode" => new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(options.OAuth2AuthorizationUrl),
                    TokenUrl = new Uri(options.OAuth2TokenUrl),
                    Scopes = options.OAuth2Scopes
                }
            },
            "clientCredentials" => new OpenApiOAuthFlows
            {
                ClientCredentials = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri(options.OAuth2TokenUrl),
                    Scopes = options.OAuth2Scopes
                }
            },
            "password" => new OpenApiOAuthFlows
            {
                Password = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri(options.OAuth2TokenUrl),
                    Scopes = options.OAuth2Scopes
                }
            },
            "implicit" => new OpenApiOAuthFlows
            {
                Implicit = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(options.OAuth2AuthorizationUrl),
                    Scopes = options.OAuth2Scopes
                }
            },
            _ => throw new NotSupportedException($"OAuth2 flow '{options.OAuth2Flow}' is not supported.")
        };

        c.AddSecurityDefinition(options.OAuth2SchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = options.OAuth2Description,
            Flows = flows
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = options.OAuth2SchemeName
                    }
                },
                options.OAuth2Scopes.Keys.ToList()
            }
        });
    }

    private static void ConfigureSwaggerEndpoints(SwaggerUIOptions uiOptions, SwaggerServiceOptions options)
    {
        foreach (var versionInfo in options.ApiVersions)
        {
            uiOptions.SwaggerEndpoint($"/swagger/{versionInfo.Version}/swagger.json", $"{versionInfo.Title} {versionInfo.Version}");
        }
    }

    private static void ApplyGeneralUiOptions(SwaggerUIOptions uiOptions, SwaggerServiceOptions options)
    {
        uiOptions.RoutePrefix = options.UiRoutePrefix;
        uiOptions.DocumentTitle = options.UiDocumentTitle ?? options.ApiVersions.FirstOrDefault()?.Title ?? "API Documentation";

        if (options.UiDisplayRequestDuration) uiOptions.DisplayRequestDuration();
        if (options.UiEnableDeepLinking) uiOptions.EnableDeepLinking();
        if (options.UiEnableFilter) uiOptions.EnableFilter();
        if (options.UiShowExtensions) uiOptions.ShowExtensions();
        if (options.UiShowCommonExtensions) uiOptions.ShowCommonExtensions();
        if (options.UiEnablePersistAuthorization) uiOptions.EnablePersistAuthorization();
        if (options.UiDisplayOperationId) uiOptions.DisplayOperationId();
        uiOptions.SupportedSubmitMethods([.. options.UiSupportedSubmitMethods.Select(s => Enum.Parse<SubmitMethod>(s, true))]);
    }
}