namespace KyrolusSous.OpenApi;

public static class OpenApiServiceExtensions
{
    public static WebApplicationBuilder AddKyrolusOpenApi(
        this WebApplicationBuilder builder,
        Action<KyrolusOpenApiOptions>? configureOptions = null)
    {
        var configSection = builder.Configuration.GetSection("KyrolusOpenApi");
        builder.Services.AddKyrolusOpenApi(configSection, configureOptions);
        return builder;
    }

    public static IServiceCollection AddKyrolusOpenApi(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KyrolusOpenApiOptions>? configureOptions = null)
    {
        var options = new KyrolusOpenApiOptions();
        configuration.Bind(options);
        configureOptions?.Invoke(options);
        return RegisterOpenApiServices(services, options);
    }

    public static IServiceCollection AddKyrolusOpenApi(
        this IServiceCollection services,
        Action<KyrolusOpenApiOptions>? configureOptions = null)
    {
        var options = new KyrolusOpenApiOptions();
        configureOptions?.Invoke(options);
        return RegisterOpenApiServices(services, options);
    }

    private static IServiceCollection RegisterOpenApiServices(
        IServiceCollection services,
        KyrolusOpenApiOptions options)
    {
        services.AddSingleton(Options.Create(options));
        services.AddEndpointsApiExplorer();

        if (options.EnableApiVersioning && options.ApiVersions.Count > 0)
        {
            foreach (var versionInfo in options.ApiVersions)
            {
                services.AddOpenApi(versionInfo.Version, openApiOptions =>
                {
                    ConfigureOpenApiDocument(openApiOptions, versionInfo, options);
                });
            }
        }
        else
        {
            var defaultVersion = ResolveDefaultVersion(options);
            services.AddOpenApi(defaultVersion.Version, openApiOptions =>
            {
                ConfigureOpenApiDocument(openApiOptions, defaultVersion, options);
            });
        }

        return services;
    }

    public static WebApplication MapKyrolusOpenApi(
        this WebApplication app,
        Action<SwaggerUIOptions>? configureSwaggerUi = null)
    {
        var options = app.Services.GetRequiredService<IOptions<KyrolusOpenApiOptions>>().Value;

        if (!app.Environment.IsDevelopment() && !options.EnableInNonDevelopmentEnvironments)
        {
            return app;
        }

        app.MapOpenApi();

        var versions = ResolveUiVersions(options);

        if (options.EnableScalarUi)
        {
            MapScalarEndpoint(app, options, versions);
        }

        if (options.EnableSwaggerUi)
        {
            MapSwaggerUiEndpoint(app, options, versions, configureSwaggerUi);
        }

        if (options.EnableReDocUi)
        {
            MapReDocEndpoint(app, options, versions);
        }

        return app;
    }

    private static void MapScalarEndpoint(WebApplication app, KyrolusOpenApiOptions options, List<ApiVersionInfo> versions)
    {
        var firstVersion = versions[0];
        app.MapScalarApiReference(options.ScalarRoutePrefix, scalarOptions =>
        {
            scalarOptions.WithOpenApiRoutePattern($"/openapi/{firstVersion.Version}.json");
            scalarOptions.WithTitle(options.UiDocumentTitle ?? firstVersion.Title);
            scalarOptions.WithTheme(options.ScalarTheme);

            if (!string.IsNullOrWhiteSpace(options.ScalarSearchHotKey))
            {
                scalarOptions.WithSearchHotKey(options.ScalarSearchHotKey);
            }

            if (!string.IsNullOrWhiteSpace(options.CustomCss))
            {
                scalarOptions.WithCustomCss(options.CustomCss);
            }

            if (!string.IsNullOrWhiteSpace(options.FaviconUrl))
            {
                scalarOptions.WithFavicon(options.FaviconUrl);
            }
        });
    }

    private static void MapSwaggerUiEndpoint(
        WebApplication app,
        KyrolusOpenApiOptions options,
        List<ApiVersionInfo> versions,
        Action<SwaggerUIOptions>? configureSwaggerUi)
    {
        app.UseSwaggerUI(swaggerUiOptions =>
        {
            swaggerUiOptions.RoutePrefix = options.SwaggerUiRoutePrefix;
            swaggerUiOptions.DocumentTitle = options.UiDocumentTitle ?? versions[0].Title;

            foreach (var version in versions)
            {
                swaggerUiOptions.SwaggerEndpoint($"/openapi/{version.Version}.json", $"{version.Title} {version.Version}");
            }

            configureSwaggerUi?.Invoke(swaggerUiOptions);
        });
    }

    private static void MapReDocEndpoint(WebApplication app, KyrolusOpenApiOptions options, List<ApiVersionInfo> versions)
    {
        var firstVersion = versions[0];
        var openApiUrl = $"/openapi/{firstVersion.Version}.json";
        var title = options.UiDocumentTitle ?? firstVersion.Title;

        app.MapGet($"/{options.ReDocRoutePrefix}", () =>
        {
            var html = $$"""
            <!DOCTYPE html>
            <html>
              <head>
                <title>{{title}}</title>
                <meta charset="utf-8"/>
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <link href="https://fonts.googleapis.com/css?family=Montserrat:300,400,700|Roboto:300,400,700" rel="stylesheet">
                <style>
                  body { margin: 0; padding: 0; }
                </style>
              </head>
              <body>
                <redoc spec-url='{{openApiUrl}}'></redoc>
                <script src="https://cdn.redoc.ly/redoc/latest/bundles/redoc.standalone.js"></script>
              </body>
            </html>
            """;

            return Results.Content(html, "text/html");
        })
        .ExcludeFromDescription();
    }

    private static void ConfigureOpenApiDocument(
        OpenApiOptions openApiOptions,
        ApiVersionInfo versionInfo,
        KyrolusOpenApiOptions options)
    {
        if (options.EnableSmartAutoTagging)
        {
            openApiOptions.AddOperationTransformer<KyrolusSmartAutoTagTransformer>();
        }

        if (options.EnableStandardErrorResponses)
        {
            openApiOptions.AddOperationTransformer<KyrolusStandardErrorResponsesTransformer>();
        }

        if (options.EnableCorrelationIdHeader)
        {
            openApiOptions.AddOperationTransformer(new KyrolusCorrelationIdHeaderTransformer(options.CorrelationIdHeaderName));
        }

        openApiOptions.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = CreateOpenApiInfo(versionInfo);
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Security ??= [];

            ApplyServers(document, options);
            ApplyJwtSecurity(document, options);
            ApplyApiKeySecurity(document, options);
            ApplyBasicSecurity(document, options);
            ApplyOAuth2Security(document, options);

            return Task.CompletedTask;
        });
    }

    private static ApiVersionInfo ResolveDefaultVersion(KyrolusOpenApiOptions options)
    {
        var existing = options.ApiVersions.FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var entryAssembly = Assembly.GetEntryAssembly();
        var assemblyName = entryAssembly?.GetName().Name ?? "API Documentation";
        var description = entryAssembly?.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;

        return new ApiVersionInfo
        {
            Version = options.Version ?? "v1",
            Title = options.Title ?? FormatTitle(assemblyName),
            Description = options.Description ?? description,
            TermsOfServiceUrl = options.TermsOfServiceUrl,
            ContactName = options.ContactName,
            ContactEmail = options.ContactEmail,
            ContactUrl = options.ContactUrl,
            LicenseName = options.LicenseName,
            LicenseUrl = options.LicenseUrl
        };
    }

    private static List<ApiVersionInfo> ResolveUiVersions(KyrolusOpenApiOptions options)
    {
        if (options.EnableApiVersioning && options.ApiVersions.Count > 0)
        {
            return options.ApiVersions;
        }

        return [ResolveDefaultVersion(options)];
    }

    private static string FormatTitle(string rawName)
    {
        var name = rawName.Replace('.', ' ');
        if (!name.EndsWith("API", StringComparison.OrdinalIgnoreCase))
        {
            name += " API";
        }
        return name;
    }

    private static OpenApiInfo CreateOpenApiInfo(ApiVersionInfo versionInfo)
    {
        return new OpenApiInfo
        {
            Title = versionInfo.Title,
            Version = versionInfo.Version,
            Description = versionInfo.Description,
            TermsOfService = ParseOptionalUri(versionInfo.TermsOfServiceUrl),
            Contact = CreateContact(versionInfo),
            License = CreateLicense(versionInfo)
        };
    }

    private static OpenApiContact? CreateContact(ApiVersionInfo versionInfo)
    {
        if (string.IsNullOrWhiteSpace(versionInfo.ContactName) && string.IsNullOrWhiteSpace(versionInfo.ContactEmail))
        {
            return null;
        }

        return new OpenApiContact
        {
            Name = versionInfo.ContactName,
            Email = versionInfo.ContactEmail,
            Url = ParseOptionalUri(versionInfo.ContactUrl)
        };
    }

    private static OpenApiLicense? CreateLicense(ApiVersionInfo versionInfo)
    {
        if (string.IsNullOrWhiteSpace(versionInfo.LicenseName))
        {
            return null;
        }

        return new OpenApiLicense
        {
            Name = versionInfo.LicenseName,
            Url = ParseOptionalUri(versionInfo.LicenseUrl)
        };
    }

    private static Uri? ParseOptionalUri(string? uriString)
    {
        return !string.IsNullOrWhiteSpace(uriString) && Uri.TryCreate(uriString, UriKind.Absolute, out var uri)
            ? uri
            : null;
    }

    private static void ApplyServers(OpenApiDocument document, KyrolusOpenApiOptions options)
    {
        if (options.Servers.Count == 0)
        {
            return;
        }

        document.Servers = options.Servers
            .Select(s => new OpenApiServer
            {
                Url = s.Url,
                Description = s.Description
            })
            .ToList();
    }

    private static void ApplyJwtSecurity(OpenApiDocument document, KyrolusOpenApiOptions options)
    {
        if (!options.EnableJwtBearerAuth)
        {
            return;
        }

        var jwtScheme = new OpenApiSecurityScheme
        {
            Description = options.JwtBearerDescription,
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };

        document.Components!.SecuritySchemes![options.JwtBearerScheme] = jwtScheme;
        document.Security!.Add(new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference(options.JwtBearerScheme), [] }
        });
    }

    private static void ApplyApiKeySecurity(OpenApiDocument document, KyrolusOpenApiOptions options)
    {
        if (!options.EnableApiKeyAuth)
        {
            return;
        }

        var apiKeyScheme = new OpenApiSecurityScheme
        {
            Description = options.ApiKeyDescription,
            Name = options.ApiKeyHeaderName,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        };

        document.Components!.SecuritySchemes![options.ApiKeySchemeName] = apiKeyScheme;
        document.Security!.Add(new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference(options.ApiKeySchemeName), [] }
        });
    }

    private static void ApplyBasicSecurity(OpenApiDocument document, KyrolusOpenApiOptions options)
    {
        if (!options.EnableBasicAuth)
        {
            return;
        }

        var basicScheme = new OpenApiSecurityScheme
        {
            Description = options.BasicAuthDescription,
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "basic"
        };

        document.Components!.SecuritySchemes![options.BasicAuthSchemeName] = basicScheme;
        document.Security!.Add(new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference(options.BasicAuthSchemeName), [] }
        });
    }

    private static void ApplyOAuth2Security(OpenApiDocument document, KyrolusOpenApiOptions options)
    {
        if (!options.EnableOAuth2Auth || string.IsNullOrWhiteSpace(options.OAuth2Flow))
        {
            return;
        }

        var flows = BuildOAuthFlows(options);
        if (flows is null)
        {
            return;
        }

        var oauthScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = options.OAuth2Description,
            Flows = flows
        };

        document.Components!.SecuritySchemes![options.OAuth2SchemeName] = oauthScheme;
        document.Security!.Add(new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference(options.OAuth2SchemeName), [.. options.OAuth2Scopes.Keys] }
        });
    }

    private static OpenApiOAuthFlows? BuildOAuthFlows(KyrolusOpenApiOptions options)
    {
        try
        {
            return options.OAuth2Flow?.ToLowerInvariant() switch
            {
                "authorizationcode" => new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = ParseOptionalUri(options.OAuth2AuthorizationUrl)!,
                        TokenUrl = ParseOptionalUri(options.OAuth2TokenUrl)!,
                        Scopes = options.OAuth2Scopes
                    }
                },
                "clientcredentials" => new OpenApiOAuthFlows
                {
                    ClientCredentials = new OpenApiOAuthFlow
                    {
                        TokenUrl = ParseOptionalUri(options.OAuth2TokenUrl)!,
                        Scopes = options.OAuth2Scopes
                    }
                },
                "password" => new OpenApiOAuthFlows
                {
                    Password = new OpenApiOAuthFlow
                    {
                        TokenUrl = ParseOptionalUri(options.OAuth2TokenUrl)!,
                        Scopes = options.OAuth2Scopes
                    }
                },
                "implicit" => new OpenApiOAuthFlows
                {
                    Implicit = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = ParseOptionalUri(options.OAuth2AuthorizationUrl)!,
                        Scopes = options.OAuth2Scopes
                    }
                },
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
