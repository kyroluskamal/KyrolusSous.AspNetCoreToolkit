namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed record KyrolusEfBulkUpdateRequest(
    QueryRequest? Request,
    Dictionary<string, object> Updates,
    bool? Cacheable = null);
