namespace KyrolusSous.EndpointKit.Core.Hateoas;

/// <summary>
/// Represents a HATEOAS hypermedia link.
/// </summary>
public sealed class KyrolusLink
{
    /// <summary>Creates a new link.</summary>
    public KyrolusLink(string rel, string href, string? method = null, string? title = null, string? type = null)
    {
        Rel = rel;
        Href = href;
        Method = method;
        Title = title;
        Type = type;
    }

    /// <summary>The relationship type (e.g., "self", "next", "prev", "related").</summary>
    public string Rel { get; }

    /// <summary>The URI of the linked resource.</summary>
    public string Href { get; }

    /// <summary>The HTTP method to use (e.g., "GET", "POST", "DELETE"). Defaults to GET if null.</summary>
    public string? Method { get; }

    /// <summary>Human-readable title for the link.</summary>
    public string? Title { get; }

    /// <summary>Media type hint for the linked resource.</summary>
    public string? Type { get; }

    /// <summary>Creates a self link.</summary>
    public static KyrolusLink Self(string href) => new("self", href, "GET");

    /// <summary>Creates a next page link.</summary>
    public static KyrolusLink Next(string href) => new("next", href, "GET", "Next page");

    /// <summary>Creates a previous page link.</summary>
    public static KyrolusLink Prev(string href) => new("prev", href, "GET", "Previous page");

    /// <summary>Creates a first page link.</summary>
    public static KyrolusLink First(string href) => new("first", href, "GET", "First page");

    /// <summary>Creates a last page link.</summary>
    public static KyrolusLink Last(string href) => new("last", href, "GET", "Last page");

    /// <summary>Creates an edit link.</summary>
    public static KyrolusLink Edit(string href) => new("edit", href, "PUT", "Edit resource");

    /// <summary>Creates a delete link.</summary>
    public static KyrolusLink Delete(string href) => new("delete", href, "DELETE", "Delete resource");

    /// <summary>Creates a related resource link.</summary>
    public static KyrolusLink Related(string rel, string href, string? title = null) => new(rel, href, "GET", title);
}

/// <summary>
/// Standard HATEOAS link relation types.
/// </summary>
public static class KyrolusLinkRel
{
    public const string Self = "self";
    public const string Next = "next";
    public const string Prev = "prev";
    public const string First = "first";
    public const string Last = "last";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Create = "create";
    public const string Collection = "collection";
    public const string Item = "item";
    public const string Parent = "parent";
    public const string Child = "child";
    public const string Related = "related";
}
