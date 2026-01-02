namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public static class KyrolusErrorCodes
{
    public const string Validation = "validation_error";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Timeout = "timeout";
    public const string ExternalService = "external_service_error";
    public const string RateLimit = "rate_limited";
    public const string BadRequest = "bad_request";
    public const string InvalidJson = "invalid_json";
    public const string DatabaseError = "database_error";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string InternalError = "internal_error";
    public const string Cancelled = "cancelled";
}
