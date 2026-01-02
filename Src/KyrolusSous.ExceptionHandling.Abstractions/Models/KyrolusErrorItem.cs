namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public sealed record KyrolusErrorItem(string? Field, string? Code, string? Message);
