using KyrolusSous.Repositories.EF.Abstractions.Query;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed record KyrolusEfBulkDeleteRequest(
    QueryRequest? Request,
    bool? Cacheable = null);
