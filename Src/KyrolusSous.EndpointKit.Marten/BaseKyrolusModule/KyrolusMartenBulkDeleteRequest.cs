using KyrolusSous.Repositories.EF.Abstractions.Query;

namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;

public sealed record KyrolusMartenBulkDeleteRequest(
    QueryRequest? Request,
    bool? Cacheable = null);
