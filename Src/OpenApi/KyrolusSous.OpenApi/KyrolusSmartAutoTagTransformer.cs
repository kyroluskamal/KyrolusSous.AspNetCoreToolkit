namespace KyrolusSous.OpenApi;

public sealed partial class KyrolusSmartAutoTagTransformer : IOpenApiOperationTransformer
{
    private static readonly Regex RouteParameterRegex = ParameterRegex();

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Tags is { Count: > 0 })
        {
            return Task.CompletedTask;
        }

        var relativePath = context.Description.RelativePath;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.CompletedTask;
        }

        var tagName = ExtractTagName(relativePath);
        if (!string.IsNullOrWhiteSpace(tagName))
        {
            operation.Tags ??= new HashSet<OpenApiTagReference>();
            operation.Tags.Add(new OpenApiTagReference(tagName));
        }

        return Task.CompletedTask;
    }

    private static string? ExtractTagName(string relativePath)
    {
        var pathOnly = relativePath.Split('?')[0];
        var segments = pathOnly.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (segment.StartsWith('v') && segment.Length > 1 && char.IsDigit(segment[1]))
            {
                continue;
            }

            if (RouteParameterRegex.IsMatch(segment))
            {
                continue;
            }

            return ToPascalCase(segment);
        }

        return segments.Length > 0 ? ToPascalCase(segments[0]) : null;
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var parts = input.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return char.ToUpperInvariant(parts[0][0]) + parts[0][1..];
        }

        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    [GeneratedRegex(@"^\{.*\}$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ParameterRegex();
}
