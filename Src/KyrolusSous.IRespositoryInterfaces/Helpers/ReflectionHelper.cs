using System.Reflection;

namespace KyrolusSous.IRespositoryInterfaces.Helpers;

public static class ReflectionHelper
{
    public static object GetPropertyValue<T>(T obj, string propertyName)
    {
        if (Equals(obj, default(T)))
            throw new ArgumentNullException(nameof(obj));

        var propInfo = obj?.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (propInfo == null)
            throw new ArgumentException($"Property {propertyName} not found on {obj?.GetType().Name}");

        return propInfo.GetValue(obj)!;
    }
}