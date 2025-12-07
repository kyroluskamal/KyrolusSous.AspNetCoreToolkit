// Src/KyrolusSous.Swagger/SwaggerServiceExtension.cs
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.SwaggerUI;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;


namespace KyrolusSous.Swagger;

public static class SwaggerServiceExtension
{
    public static void AddSwaggerService(this IServiceCollection services, Action<SwaggerServiceOptions>? configureOptions = null)
    {
        // Register SwaggerServiceOptions with DI, allowing configuration
        services.AddOptions<SwaggerServiceOptions>()
                .Configure(options => configureOptions?.Invoke(options));

        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(c =>
        {
            // Resolve options here to apply them to SwaggerGen
            var options = services.BuildServiceProvider().GetRequiredService<IOptions<SwaggerServiceOptions>>().Value;

            ApiVersioning(c, options);
            if (options.EnableXmlComments) EnableXmlComments(c, options);
            if (options.EnableJwtBearerAuth) EnableJwtBearerAuth(c, options);
            if (options.EnableOAuth2Auth) EnableOAuth2Auth(c, options);
            if (options.EnableAnnotations) c.EnableAnnotations();
            if (options.EnableNullableReferenceTypesSupport) c.SupportNonNullableReferenceTypes();
        });
    }

    public static void UseConfiguredSwaggerUI(this WebApplication app, Action<SwaggerUIOptions>? configureOptions = null)
    {
        // Resolve options from DI
        var options = app.Services.GetRequiredService<IOptions<SwaggerServiceOptions>>().Value;

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
    private static void ApiVersioning(SwaggerGenOptions c, SwaggerServiceOptions options)
    {
        if (options.EnableApiVersioning && options.ApiVersions.Any())
        {
            foreach (var versionInfo in options.ApiVersions)
            {
                AddSwaggerVersion(c, versionInfo);
            }
        }
        else
        {
            // Ensure a default v1 doc if versioning is disabled or no versions are explicitly provided
            var defaultVersionInfo = options.ApiVersions.FirstOrDefault() ?? new ApiVersionInfo();
            AddSwaggerVersion(c, new ApiVersionInfo
            {
                Version = "v1",
                Title = defaultVersionInfo.Title,
                Description = defaultVersionInfo.Description,
                ContactName = defaultVersionInfo.ContactName,
                ContactEmail = defaultVersionInfo.ContactEmail,
                ContactUrl = defaultVersionInfo.ContactUrl,
                LicenseName = defaultVersionInfo.LicenseName,
                LicenseUrl = defaultVersionInfo.LicenseUrl,
                TermsOfServiceUrl = defaultVersionInfo.TermsOfServiceUrl
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
        // Prioritize explicit absolute paths
        foreach (var absolutePath in options.XmlDocAbsolutePaths)
        {
            if (File.Exists(absolutePath))
            {
                c.IncludeXmlComments(absolutePath);
            }
            else
            {
                Console.WriteLine($"[SwaggerService] Warning: XML documentation file not found at absolute path: {absolutePath}");
            }
        }

        // Fallback to assemblies if no absolute paths are provided or if specified
        var assembliesToProcess = options.XmlCommentAssemblies.Any() ? options.XmlCommentAssemblies : [Assembly.GetEntryAssembly()!];
        foreach (var assembly in assembliesToProcess)
        {
            var xmlFile = $"{assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
            else
            {
                Console.WriteLine($"[SwaggerService] Warning: XML documentation file not found for assembly '{assembly.GetName().Name}' at default path: {xmlPath}");
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

        var jwtBearerReference = new OpenApiSecuritySchemeReference(options.JwtBearerScheme, hostDocument: null, externalResource: null);

        c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            {
                jwtBearerReference,
                new List<string>()
            }
        });
    }

    private static void EnableOAuth2Auth(SwaggerGenOptions c, SwaggerServiceOptions options)
    {
        OpenApiOAuthFlows? flows = null;
        try
        {
            flows = options.OAuth2Flow?.ToLowerInvariant() switch
            {
                "authorizationcode" => new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = !string.IsNullOrEmpty(options.OAuth2AuthorizationUrl) ? new Uri(options.OAuth2AuthorizationUrl) : throw new ArgumentException("OAuth2AuthorizationUrl is required for authorizationCode flow."),
                        TokenUrl = !string.IsNullOrEmpty(options.OAuth2TokenUrl) ? new Uri(options.OAuth2TokenUrl) : throw new ArgumentException("OAuth2TokenUrl is required for authorizationCode flow."),
                        Scopes = options.OAuth2Scopes
                    }
                },
                "clientcredentials" => new OpenApiOAuthFlows
                {
                    ClientCredentials = new OpenApiOAuthFlow
                    {
                        TokenUrl = !string.IsNullOrEmpty(options.OAuth2TokenUrl) ? new Uri(options.OAuth2TokenUrl) : throw new ArgumentException("OAuth2TokenUrl is required for clientCredentials flow."),
                        Scopes = options.OAuth2Scopes
                    }
                },
                "password" => new OpenApiOAuthFlows
                {
                    Password = new OpenApiOAuthFlow
                    {
                        TokenUrl = !string.IsNullOrEmpty(options.OAuth2TokenUrl) ? new Uri(options.OAuth2TokenUrl) : throw new ArgumentException("OAuth2TokenUrl is required for password flow."),
                        Scopes = options.OAuth2Scopes
                    }
                },
                "implicit" => new OpenApiOAuthFlows
                {
                    Implicit = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = !string.IsNullOrEmpty(options.OAuth2AuthorizationUrl) ? new Uri(options.OAuth2AuthorizationUrl) : throw new ArgumentException("OAuth2AuthorizationUrl is required for implicit flow."),
                        Scopes = options.OAuth2Scopes
                    }
                },
                _ => null // Return null for unsupported flow, will be handled below
            };
        }
        catch (ArgumentException ex) // Catch specific ArgumentException for missing URLs
        {
            Console.Error.WriteLine($"[SwaggerService] OAuth2 Configuration Error: {ex.Message}");
            return;
        }
        catch (UriFormatException ex)
        {
            // Log the error but don't throw, allowing Swagger to initialize without this specific auth
            Console.Error.WriteLine($"[SwaggerService] Invalid OAuth2 URL format: {ex.Message}");
            return;
        }


        if (flows == null)
        {
            Console.Error.WriteLine($"[SwaggerService] Unsupported OAuth2 flow '{options.OAuth2Flow}'. OAuth2 authentication will not be configured.");
            return;// Do not configure OAuth2 if flow is unsupported
        }

        c.AddSecurityDefinition(options.OAuth2SchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = options.OAuth2Description,
            Flows = flows
        });

        var oauthReference = new OpenApiSecuritySchemeReference(options.OAuth2SchemeName, hostDocument: null, externalResource: null);

        c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            {
                oauthReference,
                options.OAuth2Scopes.Keys.ToList()
            }
        });
    }

    private static void ConfigureSwaggerEndpoints(SwaggerUIOptions uiOptions, SwaggerServiceOptions options)
    {
        if (options.EnableApiVersioning && options.ApiVersions.Any())
        {
            foreach (var versionInfo in options.ApiVersions)
            {
                uiOptions.SwaggerEndpoint($"/swagger/{versionInfo.Version}/swagger.json", $"{versionInfo.Title} {versionInfo.Version}");
            }
        }
        else
        {
            var defaultVersionInfo = options.ApiVersions.FirstOrDefault() ?? new ApiVersionInfo(); //Ensure fallback
            uiOptions.SwaggerEndpoint($"/swagger/v1/swagger.json", $"{defaultVersionInfo.Title} v1");
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
