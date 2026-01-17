using KyrolusSous.Repositories.EF.Abstractions.Query;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed record KyrolusEfPagedQueryRequest(
    QueryRequest? Request,
    int? PageNumber = null,
    int? PageSize = null,
    bool? Cacheable = null,
    bool? IncludeDeleted = null);
