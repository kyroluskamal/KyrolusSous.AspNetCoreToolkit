namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed record KyrolusEfBulkPatchItem(
    string? Id,
    string[]? Keys,
    Dictionary<string, object>? Updates);
