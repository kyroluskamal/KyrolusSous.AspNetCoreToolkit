using FluentValidation;
using KyrolusSous.ExceptionHandling.Abstractions.Models;

namespace KyrolusSous.ExceptionHandling.FluentValidation;

/// <summary>
/// Configuration options for FluentValidation exception mapping in the toolkit.
/// </summary>
public sealed class KyrolusFluentValidationOptions
{
    /// <summary>
    /// Gets or sets the default summary title for validation failures (default: "Validation failed").
    /// </summary>
    public string DefaultTitle { get; set; } = "Validation failed";

    /// <summary>
    /// Gets or sets a value indicating whether to generate dynamic, contextual details
    /// containing field names and error counts (default: <c>true</c>).
    /// </summary>
    public bool EnableDynamicDetail { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional custom delegate to format the detail message from the caught <see cref="ValidationException"/>.
    /// When specified, overrides automatic dynamic detail generation.
    /// </summary>
    public Func<ValidationException, IReadOnlyList<KyrolusErrorItem>, string?>? DetailFormatter { get; set; }
}
