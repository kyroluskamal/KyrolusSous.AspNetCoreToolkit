namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed record KyrolusEfQueryParameters(
    string? Filter = null,
    string? OrderBy = null,
    string? Includes = null,
    string? IncludeGraph = null,
    string? Fields = null,
    int? PageNumber = null,
    int? PageSize = null,
    bool? Cacheable = null,
    bool? AsNoTracking = null,
    bool? UseSplitQuery = null,
    bool? IncludeDeleted = null);
