using System.Text.Json;
using KyrolusSous.ExceptionHandling.Handlers;
using Marten;
using static KyrolusSous.StaticFunctions.EntityAdministrationPropNames;
namespace KyrolusSous.StaticFunctions;

public static class KyrolusRepoistoryHelpers<TEntity> where TEntity : class
{
    public static dynamic? GetPropertyValues(TEntity entity, string propertyName)
    {
        return entity?.GetType().GetProperty(propertyName)?.GetValue(entity);
    }
    public static void SetProperty(TEntity entity, string propertyName, dynamic value)
    {
        if (entity == null || string.IsNullOrEmpty(propertyName))
            return;

        var propertyInfo = entity.GetType().GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase
        );

        if (propertyInfo == null || !propertyInfo.CanWrite)
            return;

        try
        {
            var convertedValue = ConvertValue(propertyInfo.PropertyType, value);
            propertyInfo.SetValue(entity, convertedValue);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to set property '{propertyName}'", ex);
        }
    }

    private static object? ConvertValue(Type targetType, object value)
    {
        if (targetType.IsArray && value is object[] valueArray)
        {
            Type elementType = targetType.GetElementType()!;
            Array convertedArray = Array.CreateInstance(elementType, valueArray.Length);
            for (int i = 0; i < valueArray.Length; i++)
            {
                convertedArray.SetValue(Convert.ChangeType(valueArray[i], elementType), i);
            }
            return convertedArray;
        }

        if (targetType.IsGenericType &&
            targetType.GetGenericTypeDefinition() == typeof(List<>) &&
            value is object[] genericArray)
        {
            Type genericArg = targetType.GetGenericArguments()[0];
            var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
            foreach (var item in genericArray)
            {
                list.Add(Convert.ChangeType(item, genericArg));
            }
            return list;
        }

        // الحالة الافتراضية للقيم الفردية
        return Convert.ChangeType(value, targetType);
    }


    public static object? ConvertJsonElement(JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            JsonValueKind.Number => ConvertJsonNumber(jsonElement),
            JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
            JsonValueKind.True or JsonValueKind.False => jsonElement.GetBoolean(),
            JsonValueKind.Null => null,
            JsonValueKind.Array => jsonElement.EnumerateArray().Select(ConvertJsonElement).Where(e => e != null).ToArray(),
            JsonValueKind.Object => jsonElement.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value)!),
            _ => throw new InvalidOperationException("Unsupported JsonElement type")
        };
    }

    public static object ConvertJsonNumber(JsonElement jsonElement)
    {
        if (jsonElement.TryGetInt32(out int intValue))
            return intValue;
        if (jsonElement.TryGetInt64(out long longValue))
            return longValue;
        if (jsonElement.TryGetDouble(out double doubleValue))
            return doubleValue;
        throw new InvalidOperationException("Unsupported number type");
    }
    public static object?[] ConvertJsonElementArray(object?[]? keyValues)
    {
        if (keyValues == null)
            return [];
        var convertedValues = new List<object>();
        foreach (var keyValue in keyValues)
        {
            var convertedValue = keyValue is JsonElement jsonElement ? ConvertJsonElement(jsonElement) : keyValue;
            convertedValues.Add(convertedValue!);
        }
        return [.. convertedValues];
    }

    public static Task RemoveHelper<TDocument>(TEntity? entity, TDocument session, bool isPermanent, object?[]? keyValues)
    where TDocument : IDocumentSession
    {
        var id = keyValues == null ? GetPropertyValues(entity!, "Id") : ConvertJsonElementArray(keyValues)[0];
        if (entity == null) throw new NotFoundException(typeof(TEntity).Name, id!.ToString()!);
        if (GetPropertyValues(entity, IsActive) as bool? == true)
            ThrowValidationError<TEntity>.ThrowValidationErrors([new(IsDeleted, $"{entity.GetType().Name} is active, you can't delete it")]);
        if (isPermanent)
            session.Delete(entity);
        else
        {
            SetProperty(entity, IsDeleted, true);
            session.Store(entity);
        }
        return Task.CompletedTask;
    }

    public static void HandleDelettion(IEnumerable<TEntity> entities, bool isPermanent, IDocumentSession session)
    {
        if (entities.Any())
        {
            if (isPermanent)
                session.Delete(entities);
            else
            {
                foreach (var entity in entities)
                {
                    entity!.GetType().GetProperty(IsActive)!.SetValue(entity, false);
                    entity.GetType().GetProperty(IsDeleted)!.SetValue(entity, true);
                }
                session.Store(entities);
            }
        }
    }
    public static string[]? GetIncludedProperties(string? includedProperties)
    {
        return string.IsNullOrEmpty(includedProperties) ? null : includedProperties.Split(',');
    }

}
