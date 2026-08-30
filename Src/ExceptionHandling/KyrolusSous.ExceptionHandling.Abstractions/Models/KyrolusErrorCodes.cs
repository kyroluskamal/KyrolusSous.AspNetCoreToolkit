namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

/// <summary>
/// Provides standard constant string identifiers for common framework and protocol error codes.
/// </summary>
public static class KyrolusErrorCodes
{
    /// <summary>Represents a request payload validation error ("validation_error").</summary>
    public const string Validation = "validation_error";

    /// <summary>Represents a resource not found error ("not_found").</summary>
    public const string NotFound = "not_found";

    /// <summary>Represents a business state or concurrency conflict ("conflict").</summary>
    public const string Conflict = "conflict";

    /// <summary>Represents an unauthenticated request error ("unauthorized").</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>Represents an unauthorized/forbidden access error ("forbidden").</summary>
    public const string Forbidden = "forbidden";

    /// <summary>Represents an operation or gateway timeout error ("timeout").</summary>
    public const string Timeout = "timeout";

    /// <summary>Represents a downstream 3rd-party service failure ("external_service_error").</summary>
    public const string ExternalService = "external_service_error";

    /// <summary>Represents an API rate limit quota exceeded error ("rate_limited").</summary>
    public const string RateLimit = "rate_limited";

    /// <summary>Represents a general client bad request error ("bad_request").</summary>
    public const string BadRequest = "bad_request";

    /// <summary>Represents a malformed JSON syntax payload error ("invalid_json").</summary>
    public const string InvalidJson = "invalid_json";

    /// <summary>Represents a general database infrastructure failure ("database_error").</summary>
    public const string DatabaseError = "database_error";

    /// <summary>Represents an optimistic concurrency race condition conflict ("concurrency_conflict").</summary>
    public const string ConcurrencyConflict = "concurrency_conflict";

    /// <summary>Represents an unexpected internal server crash ("internal_error").</summary>
    public const string InternalError = "internal_error";

    /// <summary>Represents a client-cancelled HTTP request ("cancelled").</summary>
    public const string Cancelled = "cancelled";
}
