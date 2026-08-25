namespace KyrolusSous.CQRS.Abstractions.Attributes;

/// <summary>
/// Specifies that the decorated CQRS request requires authorization before execution.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class KyrolusAuthorizeAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a comma-separated list of roles allowed to execute the request.
    /// </summary>
    public string? Roles { get; set; }

    /// <summary>
    /// Gets or sets a policy name that must be satisfied to execute the request.
    /// </summary>
    public string? Policy { get; set; }

    /// <summary>
    /// Gets or sets a comma-separated list of permissions required to execute the request.
    /// </summary>
    public string? Permissions { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusAuthorizeAttribute"/>.
    /// </summary>
    public KyrolusAuthorizeAttribute() { }

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusAuthorizeAttribute"/> with a policy name.
    /// </summary>
    public KyrolusAuthorizeAttribute(string policy) => Policy = policy;
}
