using KyrolusSous.Repositories.EF.Abstractions.Query;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed record KyrolusEfSeekQueryRequest(
    QueryRequest? Request,
    int? PageSize = null,
    string? Cursor = null,
    bool? Cacheable = null,
    bool? IncludeDeleted = null,
    bool? IncludeTotalCount = null,
    bool? Descending = null);
