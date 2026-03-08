using System.Reflection;
using System.Linq.Expressions;
using System.Text.Json;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

internal static class TestQueryContractBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public static Task<IResult> InvokeHandleQueryAsync<TModel, TResponse, TKey>(
        IKyrolusMartenCommandQueryHandler<TModel, TResponse, TKey> handler,
        TestQueryRequest? request,
        bool? cacheable,
        bool? includeDeleted,
        CancellationToken cancellationToken)
        where TModel : class
        where TResponse : class
        where TKey : IEquatable<TKey>
    {
        var method = handler.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == "HandleQueryAsync" && m.GetParameters().Length == 4);

        var runtimeRequestType = method.GetParameters()[0].ParameterType;
        var runtimeRequest = ConvertViaJson(request, runtimeRequestType);
        var task = (Task<IResult>)method.Invoke(handler, [runtimeRequest, cacheable, includeDeleted, cancellationToken])!;
        return task;
    }

    public static bool TryBuildClauseFilterExpression<TEntity>(
        TestFilterClause[]? clauses,
        ISet<string>? allowedProperties,
        bool strict,
        bool caseInsensitive,
        out Expression<Func<TEntity, bool>>? expression,
        out string? error)
    {
        expression = null;
        error = null;

        var method = typeof(KyrolusSous.EndpointKit.Marten.FilterBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m =>
            {
                if (m.Name != "TryBuildFilterExpression" || !m.IsGenericMethodDefinition)
                {
                    return false;
                }

                var parameters = m.GetParameters();
                return parameters.Length == 6
                    && parameters[0].ParameterType.IsGenericType
                    && parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);
            })
            .MakeGenericMethod(typeof(TEntity));

        var clauseType = method.GetParameters()[0].ParameterType.GetGenericArguments()[0];
        var runtimeClauses = ConvertViaJson(clauses, clauseType.MakeArrayType());
        object?[] args = [runtimeClauses, allowedProperties, strict, caseInsensitive, null, null];
        var success = (bool)method.Invoke(null, args)!;
        expression = args[4] as Expression<Func<TEntity, bool>>;
        error = args[5] as string;
        return success;
    }

    private static object? ConvertViaJson(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize(json, targetType, JsonOptions);
    }
}
