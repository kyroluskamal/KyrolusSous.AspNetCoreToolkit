namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public sealed record KyrolusExceptionMapping(
    KyrolusErrorEnvelope Error,
    HttpStatusCode StatusCode,
    bool IsTransient = false,
    bool ShouldLog = true);
