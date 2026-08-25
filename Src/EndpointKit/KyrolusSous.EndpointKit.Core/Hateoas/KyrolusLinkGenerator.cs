using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace KyrolusSous.EndpointKit.Core.Hateoas;

/// <summary>
/// Provides HATEOAS link generation for endpoint responses.
/// </summary>
public interface IKyrolusLinkGenerator
{
    /// <summary>Generates links for a single resource.</summary>
    IReadOnlyList<KyrolusLink> GenerateItemLinks<TResponse, TKey>(
        HttpContext context,
        IKyrolusApiConfig<TResponse> config,
        TKey id,
        TResponse item) where TResponse : class;

    /// <summary>Generates links for a collection response.</summary>
    IReadOnlyList<KyrolusLink> GenerateCollectionLinks<TResponse>(
        HttpContext context,
        IKyrolusApiConfig<TResponse> config,
        int? pageNumber = null,
        int? pageSize = null,
        long? totalCount = null) where TResponse : class;

    /// <summary>Generates links for a paged response.</summary>
    IReadOnlyList<KyrolusLink> GeneratePagedLinks<TResponse>(
        HttpContext context,
        IKyrolusApiConfig<TResponse> config,
        int pageNumber,
        int pageSize,
        long totalCount) where TResponse : class;
}

/// <summary>
/// Default HATEOAS link generator implementation.
/// </summary>
public class KyrolusDefaultLinkGenerator : IKyrolusLinkGenerator
{
    private readonly LinkGenerator _linkGenerator;

    public KyrolusDefaultLinkGenerator(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public IReadOnlyList<KyrolusLink> GenerateItemLinks<TResponse, TKey>(
        HttpContext context,
        IKyrolusApiConfig<TResponse> config,
        TKey id,
        TResponse item) where TResponse : class
    {
        var links = new List<KyrolusLink>();
        var baseUrl = GetBaseUrl(context, config);

        // Self link
        links.Add(KyrolusLink.Self($"{baseUrl}/{id}"));

        // Edit link (if update endpoint is enabled)
        if (IsEndpointEnabled(config, EndpointNames.Update))
        {
            links.Add(KyrolusLink.Edit($"{baseUrl}/{id}"));
        }

        // Delete link (if delete endpoint is enabled)
        if (IsEndpointEnabled(config, EndpointNames.Delete))
        {
            links.Add(KyrolusLink.Delete($"{baseUrl}/{id}"));
        }

        // Collection link
        links.Add(new KyrolusLink(KyrolusLinkRel.Collection, $"{baseUrl}s", "GET", "View all"));

        return links;
    }

    public IReadOnlyList<KyrolusLink> GenerateCollectionLinks<TResponse>(
        HttpContext context,
        IKyrolusApiConfig<TResponse> config,
        int? pageNumber = null,
        int? pageSize = null,
        long? totalCount = null) where TResponse : class
    {
        var links = new List<KyrolusLink>();
        var baseUrl = GetBaseUrl(context, config);

        // Self link
        var selfUrl = BuildCollectionUrl(baseUrl, pageNumber, pageSize, context.Request.QueryString.Value);
        links.Add(KyrolusLink.Self(selfUrl));

        // Create link (if add endpoint is enabled)
        if (IsEndpointEnabled(config, EndpointNames.Add))
        {
            links.Add(new KyrolusLink(KyrolusLinkRel.Create, $"{baseUrl}s", "POST", "Create new"));
        }

        // Pagination links if applicable
        if (pageNumber.HasValue && pageSize.HasValue && totalCount.HasValue)
        {
            var additionalLinks = GeneratePagedLinks(context, config, pageNumber.Value, pageSize.Value, totalCount.Value);
            foreach (var link in additionalLinks.Where(l => l.Rel != KyrolusLinkRel.Self))
            {
                links.Add(link);
            }
        }

        return links;
    }

    public IReadOnlyList<KyrolusLink> GeneratePagedLinks<TResponse>(
        HttpContext context,
        IKyrolusApiConfig<TResponse> config,
        int pageNumber,
        int pageSize,
        long totalCount) where TResponse : class
    {
        var links = new List<KyrolusLink>();
        var baseUrl = GetBaseUrl(context, config) + "s";
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        // Self link
        links.Add(KyrolusLink.Self($"{baseUrl}?pageNumber={pageNumber}&pageSize={pageSize}"));

        // First page
        if (totalPages > 0)
        {
            links.Add(KyrolusLink.First($"{baseUrl}?pageNumber=1&pageSize={pageSize}"));
        }

        // Previous page
        if (pageNumber > 1)
        {
            links.Add(KyrolusLink.Prev($"{baseUrl}?pageNumber={pageNumber - 1}&pageSize={pageSize}"));
        }

        // Next page
        if (pageNumber < totalPages)
        {
            links.Add(KyrolusLink.Next($"{baseUrl}?pageNumber={pageNumber + 1}&pageSize={pageSize}"));
        }

        // Last page
        if (totalPages > 0)
        {
            links.Add(KyrolusLink.Last($"{baseUrl}?pageNumber={totalPages}&pageSize={pageSize}"));
        }

        return links;
    }

    private static string GetBaseUrl<TResponse>(HttpContext context, IKyrolusApiConfig<TResponse> config)
        where TResponse : class
    {
        var scheme = context.Request.Scheme;
        var host = context.Request.Host;
        var pathBase = context.Request.PathBase;

        var prefix = config.Prefix;
        if (config.AppendVersionToPrefix && !string.IsNullOrEmpty(config.ApiVersion))
        {
            prefix = $"{prefix}/{config.VersionPrefix}{config.ApiVersion}";
        }

        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(pathBase.Value)) segments.Add(pathBase.Value.Trim('/'));
        if (!string.IsNullOrWhiteSpace(prefix)) segments.Add(prefix.Trim('/'));
        if (!string.IsNullOrWhiteSpace(config.Route)) segments.Add(config.Route.Trim('/'));
        var relativePath = string.Join('/', segments);

        return $"{scheme}://{host}/{relativePath}";
    }

    private static string BuildCollectionUrl(string baseUrl, int? pageNumber, int? pageSize, string? existingQuery)
    {
        var url = $"{baseUrl}s";
        var queryParams = new List<string>();

        if (pageNumber.HasValue)
            queryParams.Add($"pageNumber={pageNumber.Value}");
        if (pageSize.HasValue)
            queryParams.Add($"pageSize={pageSize.Value}");

        // Preserve other query parameters
        if (!string.IsNullOrEmpty(existingQuery))
        {
            var existing = existingQuery.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.StartsWith("pageNumber=", StringComparison.OrdinalIgnoreCase) &&
                           !p.StartsWith("pageSize=", StringComparison.OrdinalIgnoreCase));
            queryParams.AddRange(existing);
        }

        return queryParams.Count > 0 ? $"{url}?{string.Join("&", queryParams)}" : url;
    }

    private static bool IsEndpointEnabled<TResponse>(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        where TResponse : class
    {
        if (config.AllEndpointsExcept.Contains(endpoint))
            return false;

        if (config.Endpoints.Contains(EndpointNames.All))
            return true;

        return config.Endpoints.Contains(endpoint);
    }
}

/// <summary>
/// HATEOAS configuration options.
/// </summary>
public class KyrolusHateoasOptions
{
    /// <summary>Enable HATEOAS link generation (default: false).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Include links in single item responses (default: true).</summary>
    public bool IncludeItemLinks { get; set; } = true;

    /// <summary>Include links in collection responses (default: true).</summary>
    public bool IncludeCollectionLinks { get; set; } = true;

    /// <summary>Include links in paged responses (default: true).</summary>
    public bool IncludePagedLinks { get; set; } = true;

    /// <summary>Property name for links in the response (default: "_links").</summary>
    public string LinksPropertyName { get; set; } = "_links";

    /// <summary>Custom link generator type (optional).</summary>
    public Type? CustomLinkGeneratorType { get; set; }
}
