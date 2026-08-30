namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

/// <summary>
/// Represents an individual field-level validation or domain error item.
/// </summary>
/// <param name="Field">The name of the property or parameter that failed validation (e.g. "Email", "Password").</param>
/// <param name="Code">An optional specific validation subcode (e.g. "required", "min_length").</param>
/// <param name="Message">The human-readable validation error message.</param>
public sealed record KyrolusErrorItem(string? Field, string? Code, string? Message);
