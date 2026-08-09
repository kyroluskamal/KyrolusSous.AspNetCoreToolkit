namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;

public sealed record KyrolusMartenBulkPatchItem(
    string? Id,
    string[]? Keys,
    Dictionary<string, object>? Updates);
