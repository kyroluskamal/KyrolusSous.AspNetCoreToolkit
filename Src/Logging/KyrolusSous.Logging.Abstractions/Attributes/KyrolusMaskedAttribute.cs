namespace KyrolusSous.Logging.Abstractions.Attributes;

/// <summary>
/// Specifies that a property contains sensitive data (e.g., passwords, credit card numbers, personal identifiers)
/// that must be sanitized or masked when logged or serialized in diagnostic pipelines.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KyrolusMaskedAttribute"/> class.
/// </remarks>
/// <param name="mask">The custom static mask replacement string. If set, replaces the entire value.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusMaskedAttribute(string? mask = "***MASKED***") : Attribute
{


    /// <summary>
    /// Gets the static mask text to use (default is <c>***MASKED***</c>).
    /// </summary>
    public string Mask { get; } = mask ?? "***MASKED***";

    /// <summary>
    /// Gets or sets the number of characters to leave visible at the start of the string (e.g., 2 for "ab***").
    /// </summary>
    public int ShowFirst { get; set; }

    /// <summary>
    /// Gets or sets the number of characters to leave visible at the end of the string (e.g., 4 for "****1234").
    /// </summary>
    public int ShowLast { get; set; }

    /// <summary>
    /// Gets or sets the masking character used when partially unmasking or preserving length (default is <c>'*'</c>).
    /// </summary>
    public char MaskCharacter { get; set; } = '*';

    /// <summary>
    /// Gets or sets a value indicating whether the masked output length should match the original input length.
    /// </summary>
    public bool PreserveLength { get; set; }
}
