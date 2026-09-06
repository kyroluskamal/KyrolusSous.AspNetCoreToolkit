namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that defends against Path Traversal and Null-Byte Injection attacks (CWE-22 / CWE-20)
/// by validating inbound request paths before routing to backend services.
/// </summary>
public sealed class KyrolusPathTraversalTransformProvider : ITransformProvider
{
    private static readonly byte[] BadRequestBytes =
        """{"type":"https://httpstatuses.com/400","title":"Bad Request","status":400,"detail":"Path traversal or invalid characters detected in the request path or query."}"""u8.ToArray();

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context) { }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) { }

    /// <summary>
    /// Attaches the path traversal security transform to the YARP transform pipeline.
    /// </summary>
    /// <param name="context">The transform builder context.</param>
    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Response.HasStarted)
            {
                return;
            }

            var request = transformContext.HttpContext.Request;
            var rawPath = request.Path.Value;
            var queryString = request.QueryString.Value;

            if (ContainsPathTraversal(rawPath) || ContainsPathTraversal(queryString))
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                transformContext.HttpContext.Response.ContentType = "application/problem+json";
                await transformContext.HttpContext.Response.Body.WriteAsync(BadRequestBytes, transformContext.HttpContext.RequestAborted);
            }
        });
    }

    internal static bool ContainsPathTraversal(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        // 1. Defend against Null-Byte Injection (%00, \0)
        if (path.Contains('\0') || path.Contains("%00", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Defend against raw unnormalized dot-segment traversals
        if (path.Contains("/../", StringComparison.Ordinal) ||
            path.EndsWith("/..", StringComparison.Ordinal) ||
            path.StartsWith("../", StringComparison.Ordinal) ||
            string.Equals(path, "..", StringComparison.Ordinal))
        {
            return true;
        }

        // 3. Defend against encoded dot segments and mixed slash encodings (..%2f, %2e%2e, ..%5c)
        if (path.Contains("..%2f", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("..%5c", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2f..", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%5c..", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%2e.", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".%2e", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 4. Defend against Windows/IIS backslash traversal bypasses
        if (path.Contains(@"\..\") || path.EndsWith(@"\..") || path.Contains(@"\"))
        {
            return true;
        }

        // 5. Defend against Semicolon / Matrix Parameter Traversal bypasses (CVE-2020-5410 / CVE-2018-1271)
        if (path.Contains("..;", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(";..", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/..;/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/;../", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/.;/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("..%3b", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%3b..", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 6. Deep inspection on unescaped content to catch double-encoded or alternate bypasses
        try
        {
            var unescaped = Uri.UnescapeDataString(path);
            if (unescaped.Contains('\0') ||
                unescaped.Contains("/../", StringComparison.Ordinal) ||
                unescaped.EndsWith("/..", StringComparison.Ordinal) ||
                unescaped.StartsWith("../", StringComparison.Ordinal) ||
                unescaped.Contains("..;", StringComparison.Ordinal) ||
                unescaped.Contains(";..", StringComparison.Ordinal) ||
                unescaped.Contains("/..;/", StringComparison.Ordinal) ||
                unescaped.Contains("/;../", StringComparison.Ordinal) ||
                unescaped.Contains("/.;/", StringComparison.Ordinal) ||
                unescaped.Contains(@"\"))
            {
                return true;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }
}
