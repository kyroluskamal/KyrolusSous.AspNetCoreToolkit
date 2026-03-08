using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;

public sealed record KyrolusMartenBulkUpdateRequest(
    QueryRequest? Request,
    Dictionary<string, object> Updates,
    bool? Cacheable = null);

