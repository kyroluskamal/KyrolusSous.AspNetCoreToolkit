using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Query;

namespace KyrolusSous.CQRS.EF.Command.Bulk;

public sealed class ExecuteUpdateCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusCommandHandler<ExecuteUpdateCommand<TResponse, TKey>, int>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<int> Handle(ExecuteUpdateCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Defense in depth: the command's constructor already rejects a null filter, and Filter is
        // init-only, but reflection (e.g. property-based command builders) can still bypass both, so
        // re-validate here before anything reaches the database.
        ArgumentNullException.ThrowIfNull(command.Filter, nameof(command.Filter));
        ArgumentNullException.ThrowIfNull(command.Updates);

        if (command.Updates.Count == 0)
        {
            return 0;
        }

        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var setters = BuildSetters(command.Updates);
        return await repo.ExecuteUpdateAsync(command.Filter, setters, command.UseSplitQuery, cancellationToken);
    }

    private static Action<UpdateSettersBuilder<TResponse>> BuildSetters(Dictionary<string, object> updates)
    {
        return setters =>
        {
            foreach (var (name, rawValue) in updates)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var prop = typeof(TResponse).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop is null || !prop.CanWrite)
                {
                    throw new InvalidOperationException($"Property '{name}' not found on '{typeof(TResponse).Name}'.");
                }

                // Always-on, independent of IKyrolusPropertyUpdateRequest.AllowedProperties (which is
                // opt-in and does nothing when a caller never sets it): a key, concurrency-token, or
                // DB-computed column must never be writable through ExecuteUpdate regardless of
                // allow-list configuration. See EfProtectedPropertyGuard, shared with Patch/BulkPatch.
                EfProtectedPropertyGuard.ThrowIfProtected(prop, typeof(TResponse), "ExecuteUpdate");

                var value = ConvertValue(rawValue, prop.PropertyType);
                var parameter = Expression.Parameter(typeof(TResponse), "e");
                var propertyAccess = Expression.Property(parameter, prop);
                var propertyLambda = Expression.Lambda(propertyAccess, parameter);

                var setMethod = typeof(UpdateSettersBuilder<TResponse>)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "SetProperty"
                        && m.IsGenericMethod
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType.IsGenericParameter);

                if (setMethod is null)
                {
                    throw new InvalidOperationException("SetProperty method not found on UpdateSettersBuilder.");
                }

                var generic = setMethod.MakeGenericMethod(prop.PropertyType);
                generic.Invoke(setters, new[] { propertyLambda, value });
            }
        };
    }

    private static object? ConvertValue(object? rawValue, Type propertyType)
    {
        if (rawValue is null)
        {
            return null;
        }

        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (targetType.IsInstanceOfType(rawValue))
        {
            return rawValue;
        }

        if (rawValue is JsonElement json)
        {
            return ConvertJsonElement(json, targetType);
        }

        if (targetType.IsEnum)
        {
            if (rawValue is string enumText)
            {
                return Enum.Parse(targetType, enumText, ignoreCase: true);
            }

            return Enum.ToObject(targetType, rawValue);
        }

        if (targetType == typeof(Guid))
        {
            if (rawValue is Guid guid)
            {
                return guid;
            }

            if (rawValue is string guidText && Guid.TryParse(guidText, out var parsed))
            {
                return parsed;
            }
        }

        if (targetType == typeof(DateTimeOffset))
        {
            if (rawValue is DateTimeOffset dto)
            {
                return dto;
            }

            if (rawValue is string dtoText && DateTimeOffset.TryParse(dtoText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed;
            }
        }

        if (targetType == typeof(DateTime))
        {
            if (rawValue is DateTime dt)
            {
                return dt;
            }

            if (rawValue is string dtText && DateTime.TryParse(dtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed;
            }
        }

        if (targetType == typeof(TimeSpan))
        {
            if (rawValue is TimeSpan ts)
            {
                return ts;
            }

            if (rawValue is string tsText && TimeSpan.TryParse(tsText, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        if (rawValue is IConvertible)
        {
            return Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
        }

        return rawValue;
    }

    private static object? ConvertJsonElement(JsonElement element, Type targetType)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (targetType == typeof(string))
        {
            return element.GetString();
        }

        if (targetType == typeof(bool))
        {
            return element.GetBoolean();
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(element.GetString() ?? string.Empty);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(element.GetString() ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(element.GetString() ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (targetType == typeof(TimeSpan))
        {
            return TimeSpan.Parse(element.GetString() ?? string.Empty, CultureInfo.InvariantCulture);
        }

        if (targetType.IsEnum)
        {
            var enumText = element.GetString();
            return enumText is null ? null : Enum.Parse(targetType, enumText, ignoreCase: true);
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            if (targetType == typeof(int)) return element.GetInt32();
            if (targetType == typeof(long)) return element.GetInt64();
            if (targetType == typeof(decimal)) return element.GetDecimal();
            if (targetType == typeof(double)) return element.GetDouble();
            if (targetType == typeof(float)) return element.GetSingle();
        }

        return JsonSerializer.Deserialize(element.GetRawText(), targetType);
    }
}
