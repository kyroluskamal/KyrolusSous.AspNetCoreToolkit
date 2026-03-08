using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;

public sealed record KyrolusMartenSeekQueryRequest(
    QueryRequest? Request,
    int? PageSize = null,
    string? Cursor = null,
    bool? Cacheable = null,
    bool? IncludeDeleted = null,
    bool? IncludeTotalCount = null,
    bool? Descending = null);

