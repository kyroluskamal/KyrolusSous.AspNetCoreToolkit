namespace KyrolusSous.Gateway.Yarp.Transforms;

/// <summary>
/// YARP transform provider that defends against Path Traversal and Null-Byte Injection attacks (CWE-22 / CWE-20)
/// by validating inbound request paths before routing to backend services.
/// </summary>
public sealed class KyrolusPathTraversalTransformProvider : ITransformProvider
{
    private static readonly byte[] BadRequestBytes =
        """{"title":"Bad Request","status":400,"detail":"Path traversal or invalid characters detected in the request path."}"""u8.ToArray();

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

            var rawPath = transformContext.HttpContext.Request.Path.Value;

            if (ContainsPathTraversal(rawPath))
            {
                transformContext.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                transformContext.HttpContext.Response.ContentType = "application/problem+json";
                await transformContext.HttpContext.Response.Body.WriteAsync(BadRequestBytes, transformContext.HttpContext.RequestAborted);
            }
        });
    }

    private static bool ContainsPathTraversal(string? path)
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

        // 3. Defend against encoded dot segments (%2e%2e, %2e., .%2e)
        if (path.Contains("%2e", StringComparison.OrdinalIgnoreCase))
        {
            if (path.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("%2e.", StringComparison.OrdinalIgnoreCase) ||
                path.Contains(".%2e", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 4. Defend against Windows/IIS backslash traversal bypasses
        if (path.Contains(@"\..\") || path.EndsWith(@"\..") || path.Contains(@"\"))
        {
            return true;
        }

        return false;
    }
}
