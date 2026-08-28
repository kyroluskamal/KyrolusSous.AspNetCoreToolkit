using System.Reflection;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.DataProtection.EntityFramework;

/// <summary>
/// ModelBuilder extensions for configuring transparent property encryption.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Scans all entity types in the model and configures <see cref="KyrolusEncryptedValueConverter"/>
    /// on all string properties decorated with <see cref="KyrolusEncryptedAttribute"/>.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="provider">The data protection provider.</param>
    /// <param name="defaultPurpose">Default purpose prefix (default: "EntityFramework.EncryptedProperties").</param>
    public static ModelBuilder UseDataProtectionEncryption(
        this ModelBuilder modelBuilder,
        IDataProtectionProvider provider,
        string defaultPurpose = "EntityFramework.EncryptedProperties")
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(provider);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (clrType is null) continue;

            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType != typeof(string)) continue;

                var propertyInfo = property.PropertyInfo;
                if (propertyInfo is null) continue;

                var encryptedAttr = propertyInfo.GetCustomAttribute<KyrolusEncryptedAttribute>();
                if (encryptedAttr is not null)
                {
                    var purpose = !string.IsNullOrWhiteSpace(encryptedAttr.Purpose)
                        ? encryptedAttr.Purpose
                        : $"{defaultPurpose}.{clrType.Name}.{propertyInfo.Name}";

                    var protector = provider.CreateProtector(purpose);
                    property.SetValueConverter(new KyrolusEncryptedValueConverter(protector));
                }
            }
        }

        return modelBuilder;
    }
}
