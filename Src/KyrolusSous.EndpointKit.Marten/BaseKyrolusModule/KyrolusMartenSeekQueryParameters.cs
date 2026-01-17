namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;

public sealed record KyrolusMartenSeekQueryParameters(
    string? Filter = null,
    string? Includes = null,
    string? IncludeGraph = null,
    string? Fields = null,
    int? PageSize = null,
    string? Cursor = null,
    bool? Cacheable = null,
    bool? AsNoTracking = null,
    bool? UseSplitQuery = null,
    bool? IncludeDeleted = null,
    bool? IncludeTotalCount = null,
    bool? Descending = null);
