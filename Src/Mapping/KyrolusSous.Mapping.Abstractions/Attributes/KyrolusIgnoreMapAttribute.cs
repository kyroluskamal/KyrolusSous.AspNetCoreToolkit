namespace KyrolusSous.Mapping.Abstractions.Attributes;

/// <summary>
/// Instructs the mapping engine and source generator to ignore the decorated member during mapping operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Preventing sensitive internal state (e.g., password hashes, concurrency tokens, internal database IDs) from being copied into outgoing DTOs:
/// <code>
/// public class User
/// {
///     public string Username { get; set; } = string.Empty;
///     
///     [KyrolusIgnoreMap]
///     public string PasswordHash { get; set; } = string.Empty;
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusIgnoreMapAttribute : Attribute
{
}
