namespace KyrolusSous.EndpointKit.EF;
public static class KyrolusSousRoutingHelpers
{
    public static List<string>? GetIncludedProperties(string? includedProperties) =>
                string.IsNullOrEmpty(includedProperties) ? null : [.. includedProperties.Split(',')];
}
