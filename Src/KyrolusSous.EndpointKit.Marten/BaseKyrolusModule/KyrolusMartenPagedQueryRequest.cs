using KyrolusSous.Repositories.EF.Abstractions.Query;

namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;

public sealed record KyrolusMartenPagedQueryRequest(
    QueryRequest? Request,
    int? PageNumber = null,
    int? PageSize = null,
    bool? Cacheable = null,
    bool? IncludeDeleted = null);
