namespace KyrolusSous.OpenApi;

/// <summary>
/// Transformer that inspects endpoint authorization metadata to attach security requirements
/// only to protected operations, remove them from anonymous endpoints, and document required roles/permissions.
/// </summary>
public sealed class KyrolusEndpointAuthorizationTransformer(KyrolusOpenApiOptions options) : IOpenApiOperationTransformer
{
    private readonly KyrolusOpenApiOptions _options = options;

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata is null || metadata.Count == 0)
        {
            if (_options.RequireAuthorizationByDefault)
            {
                ApplySecurityRequirements(operation);
            }
            return Task.CompletedTask;
        }

        // 1. [AllowAnonymous] takes highest precedence
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security?.Clear();
            return Task.CompletedTask;
        }

        // 2. Check for [Authorize] or default policy
        var authData = metadata.OfType<IAuthorizeData>().ToList();
        var isAuthorized = authData.Count > 0 || _options.RequireAuthorizationByDefault;

        if (isAuthorized)
        {
            ApplySecurityRequirements(operation);

            // Document required roles
            var roles = authData
                .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
                .Select(a => a.Roles!)
                .ToList();

            if (roles.Count > 0)
            {
                var rolesAnnotation = $"\n\n**Required Roles:** {string.Join(", ", roles)}";
                operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                    ? rolesAnnotation.TrimStart()
                    : operation.Description + rolesAnnotation;
            }

            // Document required policies
            var policies = authData
                .Where(a => !string.IsNullOrWhiteSpace(a.Policy))
                .Select(a => a.Policy!)
                .ToList();

            if (policies.Count > 0)
            {
                var policiesAnnotation = $"\n\n**Required Policy:** {string.Join(", ", policies)}";
                operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                    ? policiesAnnotation.TrimStart()
                    : operation.Description + policiesAnnotation;
            }

            // Document required permissions dynamically (decoupled from Auth.Permissions)
            AnnotatePermissions(operation, metadata);
        }

        return Task.CompletedTask;
    }

    private void ApplySecurityRequirements(OpenApiOperation operation)
    {
        operation.Security ??= [];

        if (_options.EnableJwtBearerAuth && !HasScheme(operation, _options.JwtBearerScheme))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(_options.JwtBearerScheme), [] }
            });
        }

        if (_options.EnableApiKeyAuth && !HasScheme(operation, _options.ApiKeySchemeName))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(_options.ApiKeySchemeName), [] }
            });
        }

        if (_options.EnableBasicAuth && !HasScheme(operation, _options.BasicAuthSchemeName))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(_options.BasicAuthSchemeName), [] }
            });
        }

        if (_options.EnableOAuth2Auth && !HasScheme(operation, _options.OAuth2SchemeName))
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(_options.OAuth2SchemeName), [.. _options.OAuth2Scopes.Keys] }
            });
        }
    }

    private static bool HasScheme(OpenApiOperation operation, string schemeName)
    {
        return operation.Security is not null && operation.Security.Any(req =>
            req.Keys.Any(k => string.Equals(k.Reference?.Id, schemeName, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AnnotatePermissions(OpenApiOperation operation, IList<object> metadata)
    {
        foreach (var item in metadata)
        {
            var type = item.GetType();
            if (type.Name.Contains("Permission", StringComparison.OrdinalIgnoreCase))
            {
                var prop = type.GetProperty("Permissions") ?? type.GetProperty("Permission");
                if (prop is not null)
                {
                    var val = prop.GetValue(item);
                    if (val is IEnumerable<string> permList)
                    {
                        var filtered = permList.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                        if (filtered.Count > 0)
                        {
                            var annotation = $"\n\n**Required Permissions:** {string.Join(", ", filtered)}";
                            operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                                ? annotation.TrimStart()
                                : operation.Description + annotation;
                        }
                    }
                    else if (val is string singlePerm && !string.IsNullOrWhiteSpace(singlePerm))
                    {
                        var annotation = $"\n\n**Required Permissions:** {singlePerm}";
                        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                            ? annotation.TrimStart()
                            : operation.Description + annotation;
                    }
                }
            }
        }
    }
}
