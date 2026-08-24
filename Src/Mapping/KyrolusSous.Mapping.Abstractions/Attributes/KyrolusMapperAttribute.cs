namespace KyrolusSous.Mapping.Abstractions.Attributes;

/// <summary>
/// Marks a partial class or partial interface as a compile-time static mapper definition for the Roslyn Source Generator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Creating strongly-typed, 0-allocation mapper contracts without runtime reflection:
/// <code>
/// [KyrolusMapper]
/// public static partial class UserMapper
/// {
///     public static partial UserDto ToDto(User user);
///     public static partial User ToEntity(UserDto dto);
/// }
/// </code>
/// The source generator will automatically emit the partial implementation at build time.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class KyrolusMapperAttribute : Attribute
{
}
