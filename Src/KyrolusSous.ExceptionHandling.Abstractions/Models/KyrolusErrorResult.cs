namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public sealed record KyrolusErrorResult(
    KyrolusErrorEnvelope Error,
    HttpStatusCode? StatusCode = null,
    bool IsTransient = false,
    string? ExceptionType = null);
