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

    public static WebApplication MapKyrolusOpenApi(this WebApplication app)
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
            MapExtensibleUiProviders(app, options, versions);
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

        if (versions.Count > 1)
        {
            foreach (var version in versions)
            {
                var v = version;
                app.MapScalarApiReference($"{options.ScalarRoutePrefix}/{v.Version}", scalarOptions =>
                {
                    scalarOptions.WithOpenApiRoutePattern($"/openapi/{v.Version}.json");
                    scalarOptions.WithTitle(options.UiDocumentTitle ?? v.Title);
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
        }
    }

    private static void MapExtensibleUiProviders(
        WebApplication app,
        KyrolusOpenApiOptions options,
        List<ApiVersionInfo> versions)
    {
        var providers = app.Services.GetServices<IKyrolusOpenApiUiProvider>().ToList();

        if (providers.Count == 0)
        {
            providers.AddRange(DiscoverUiProviders());
        }

        foreach (var provider in providers)
        {
            provider.MapUi(app, options, versions);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Discovery of optional UI provider")]
    private static List<IKyrolusOpenApiUiProvider> DiscoverUiProviders()
    {
        var list = new List<IKyrolusOpenApiUiProvider>();
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

            if (!assemblies.Any(a => a.FullName?.StartsWith("KyrolusSous.OpenApi.SwaggerUI", StringComparison.OrdinalIgnoreCase) == true))
            {
                try
                {
                    var loaded = Assembly.Load("KyrolusSous.OpenApi.SwaggerUI");
                    assemblies.Add(loaded);
                }
                catch
                {
                    // Package not present
                }
            }

            foreach (var asm in assemblies)
            {
                if (asm.FullName?.StartsWith("KyrolusSous.OpenApi.SwaggerUI", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var type = asm.GetType("KyrolusSous.OpenApi.SwaggerUI.KyrolusSwaggerUiProvider");
                    if (type is not null && typeof(IKyrolusOpenApiUiProvider).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type) is IKyrolusOpenApiUiProvider instance)
                        {
                            list.Add(instance);
                        }
                    }
                }
            }
        }
        catch
        {
            // Graceful best-effort discovery
        }
        return list;
    }

    private static void MapReDocEndpoint(WebApplication app, KyrolusOpenApiOptions options, List<ApiVersionInfo> versions)
    {
        if (versions.Count == 0)
        {
            return;
        }

        var routePrefix = string.IsNullOrWhiteSpace(options.ReDocRoutePrefix) ? "redoc" : options.ReDocRoutePrefix.Trim('/');
        var firstVersion = versions[0];
        var openApiUrl = $"/openapi/{firstVersion.Version}.json";
        var title = options.UiDocumentTitle ?? firstVersion.Title;

        string BuildReDocHtml(string specUrl, string docTitle)
        {
            var encodedDocTitle = System.Net.WebUtility.HtmlEncode(docTitle);
            var encodedSpecUrl = System.Net.WebUtility.HtmlEncode(specUrl);

            var versionLinks = versions.Count > 1
                ? string.Join(" | ", versions.Select(v => $"<a href=\"/{System.Net.WebUtility.HtmlEncode(routePrefix)}/{System.Net.WebUtility.HtmlEncode(v.Version)}\" style=\"color: #007acc; text-decoration: none; font-weight: bold; margin: 0 5px;\">{System.Net.WebUtility.HtmlEncode(v.Version)}</a>"))
                : "";

            var navBar = versions.Count > 1
                ? $"""<div style="background-color: #f8f9fa; padding: 8px 16px; border-bottom: 1px solid #e9ecef; font-family: sans-serif; font-size: 14px;"><strong>Versions:</strong> {versionLinks}</div>"""
                : "";

            return $$"""
            <!DOCTYPE html>
            <html>
              <head>
                <title>{{encodedDocTitle}}</title>
                <meta charset="utf-8"/>
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <link href="https://fonts.googleapis.com/css?family=Montserrat:300,400,700|Roboto:300,400,700" rel="stylesheet">
                <style>
                  body { margin: 0; padding: 0; }
                </style>
              </head>
              <body>
                {{navBar}}
                <redoc spec-url='{{encodedSpecUrl}}'></redoc>
                <script src="https://cdn.redoc.ly/redoc/latest/bundles/redoc.standalone.js"></script>
              </body>
            </html>
            """;
        }

        app.MapGet($"/{routePrefix}", () => Results.Content(BuildReDocHtml(openApiUrl, title), "text/html"))
            .ExcludeFromDescription();

        if (versions.Count > 1)
        {
            foreach (var version in versions)
            {
                var v = version;
                app.MapGet($"/{routePrefix}/{v.Version}", () =>
                {
                    var vUrl = $"/openapi/{v.Version}.json";
                    var vTitle = options.UiDocumentTitle ?? v.Title;
                    return Results.Content(BuildReDocHtml(vUrl, vTitle), "text/html");
                }).ExcludeFromDescription();
            }
        }
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
            openApiOptions.AddOperationTransformer(new KyrolusStandardErrorResponsesTransformer(options));
        }

        if (options.EnableCorrelationIdHeader)
        {
            openApiOptions.AddOperationTransformer(new KyrolusCorrelationIdHeaderTransformer(options.CorrelationIdHeaderName));
        }

        if (options.EnableTenantIdHeader)
        {
            openApiOptions.AddOperationTransformer(new KyrolusTenantIdHeaderTransformer(options.TenantIdHeaderName, options.TenantIdDescription));
        }

        if (options.EnableSmartAuthorization)
        {
            openApiOptions.AddOperationTransformer(new KyrolusEndpointAuthorizationTransformer(options));
        }

        if (options.SortTagsAlphabetically)
        {
            openApiOptions.AddDocumentTransformer(new KyrolusTagOrderDocumentTransformer(options));
        }

        if (options.EnableDeprecationTransformer)
        {
            openApiOptions.AddOperationTransformer<KyrolusDeprecationOperationTransformer>();
        }

        if (options.EnableRateLimitingTransformer)
        {
            openApiOptions.AddOperationTransformer(new KyrolusRateLimitingResponseTransformer(options));
        }

        if (options.EnableXmlComments)
        {
            openApiOptions.AddOperationTransformer(new KyrolusXmlDocumentationTransformer(options));
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

        options.ConfigureOpenApiOptions?.Invoke(openApiOptions);
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
        if (!options.EnableSmartAuthorization)
        {
            document.Security!.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(options.JwtBearerScheme), [] }
            });
        }
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
        if (!options.EnableSmartAuthorization)
        {
            document.Security!.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(options.ApiKeySchemeName), [] }
            });
        }
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
        if (!options.EnableSmartAuthorization)
        {
            document.Security!.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(options.BasicAuthSchemeName), [] }
            });
        }
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
        if (!options.EnableSmartAuthorization)
        {
            document.Security!.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(options.OAuth2SchemeName), [.. options.OAuth2Scopes.Keys] }
            });
        }
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

    /// <summary>
    /// Exports the generated OpenAPI JSON specification to a destination file.
    /// </summary>
    /// <param name="app">The WebApplication instance.</param>
    /// <param name="outputPath">The file destination path to write the JSON specification.</param>
    /// <param name="documentName">The OpenAPI document version or name (default is "v1").</param>
    /// <param name="httpClient">Optional HttpClient to use for fetching the specification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SaveOpenApiDocumentAsync(
        this WebApplication app,
        string outputPath,
        string documentName = "v1",
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var docName = string.IsNullOrWhiteSpace(documentName) ? "v1" : documentName.TrimStart('/');
        var client = httpClient ?? new HttpClient
        {
            BaseAddress = ResolveBaseAddress(app)
        };

        try
        {
            var response = await client.GetAsync($"/openapi/{docName}.json", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            await File.WriteAllTextAsync(outputPath, content, cancellationToken);
        }
        finally
        {
            if (httpClient is null)
            {
                client.Dispose();
            }
        }
    }

    private static Uri ResolveBaseAddress(WebApplication app)
    {
        var rawUrl = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
        rawUrl = rawUrl.Replace("0.0.0.0", "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                       .Replace("*", "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                       .Replace("+", "127.0.0.1", StringComparison.OrdinalIgnoreCase);

        return Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)
            ? uri
            : new Uri("http://localhost:5000");
    }
}
