namespace KyrolusSous.DataProtection.Abstractions;

/// <summary>
/// Specifies that the decorated property should be transparently encrypted when persisted to the database.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusEncryptedAttribute : Attribute
{
    /// <summary>
    /// Optional purpose string to use for this property's protector.
    /// If null or whitespace, a default purpose based on entity and property name is used.
    /// </summary>
    public string? Purpose { get; }

    public KyrolusEncryptedAttribute(string? purpose = null)
    {
        Purpose = purpose;
    }
}
