using System.Diagnostics.CodeAnalysis;
using KyrolusSous.Logging.Abstractions.Attributes;
using KyrolusSous.Logging.Core.Masking;
using Serilog.Core;
using Serilog.Events;

namespace KyrolusSous.Logging.Serilog.Destructuring;

/// <summary>
/// Serilog destructuring policy that automatically masks sensitive properties and PII during structured object serialization.
/// </summary>
public sealed class KyrolusSerilogDestructuringPolicy(IKyrolusDataMasker? masker = null) : IDestructuringPolicy
{
    private readonly IKyrolusDataMasker _masker = masker ?? new KyrolusSensitiveDataMasker();

    /// <inheritdoc/>
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        var type = value.GetType();

        // Skip primitive types, common scalar types, strings, and standard collection structures
        if (type.IsPrimitive || type.IsEnum || value is string || value is decimal || value is Guid ||
            value is DateTime || value is DateTimeOffset || value is TimeSpan ||
            value is DateOnly || value is TimeOnly || value is Uri || value is System.Collections.IEnumerable)
        {
            result = null;
            return false;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        var logProperties = new List<LogEventProperty>();

        foreach (var prop in properties)
        {
            object? rawValue;
            try
            {
                rawValue = prop.GetValue(value);
            }
            catch (Exception ex)
            {
                rawValue = $"<error reading property: {ex.GetType().Name}>";
            }

            var maskedAttr = prop.GetCustomAttribute<KyrolusMaskedAttribute>();

            LogEventPropertyValue propVal;

            if (maskedAttr is not null || _masker.IsSensitivePropertyName(prop.Name))
            {
                var maskedText = _masker.MaskString(rawValue?.ToString(), maskedAttr);
                propVal = new ScalarValue(maskedText);
            }
            else
            {
                propVal = propertyValueFactory.CreatePropertyValue(rawValue, destructureObjects: true);
            }

            logProperties.Add(new LogEventProperty(prop.Name, propVal));
        }

        result = new StructureValue(logProperties, type.Name);
        return true;
    }
}
