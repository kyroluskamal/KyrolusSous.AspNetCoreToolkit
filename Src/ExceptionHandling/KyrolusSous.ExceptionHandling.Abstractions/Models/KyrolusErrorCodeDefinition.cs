namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public sealed record KyrolusErrorCodeDefinition(
    string Code,
    string Title,
    HttpStatusCode StatusCode,
    string? Description = null);
