using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

internal sealed class RuntimeNoopMapper : IKyrolusMapper
{
    public dynamic MapResponseToViewModel<TResponse>(TResponse model, Type viewModel, int statusCode, string message = "Success")
        where TResponse : class
        => model;

    public dynamic MapModelToEntity<TModel, TResponse>(TModel model)
        where TModel : class
        => model;

    public dynamic MapModelToEntity<TModel, TResponse>(IEnumerable<TModel> model)
        where TModel : class
        => model.ToList();
}

internal sealed class RuntimeMediatorStub : IKyrolusMediatorSender
{
    private readonly Func<object, object?> responder;

    public RuntimeMediatorStub(Func<object, object?>? responder = null)
    {
        this.responder = responder ?? (_ => null);
    }

    public Task<TResponse> SendAsync<TResponse>(IKyrolusQuery<TResponse> query, CancellationToken cancellationToken = default)
        => Task.FromResult((TResponse)responder(query)!);

    public Task<TResponse> SendAsync<TResponse>(IKyrolusRequest<TResponse> request, CancellationToken cancellationToken = default)
        => Task.FromResult((TResponse)responder(request)!);

    public Task SendAsync(IKyrolusCommand command, CancellationToken cancellationToken = default)
    {
        _ = responder(command);
        return Task.CompletedTask;
    }

    public Task<TResponse> SendAsync<TResponse>(IKyrolusCommand<TResponse> command, CancellationToken cancellationToken = default)
        => Task.FromResult((TResponse?)responder(command)!);

    public async IAsyncEnumerable<TResponse> StreamAsync<TResponse>(IKyrolusStreamRequest<TResponse> request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

internal static class RuntimeDefaultCommandQueryHandlerProbe<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    private static readonly Type HandlerType = typeof(DefaultCommandQueryHandler<TResponse, TModel, TKey>);

    public static DefaultCommandQueryHandler<TResponse, TModel, TKey> Create(
        IKyrolusApiConfig<TResponse> config,
        IServiceProvider? serviceProvider = null,
        IKyrolusMapper? mapper = null,
        IKyrolusMediatorSender? mediator = null)
    {
        return new DefaultCommandQueryHandler<TResponse, TModel, TKey>(
            mapper ?? new RuntimeNoopMapper(),
            mediator ?? new RuntimeMediatorStub(),
            config,
            serviceProvider ?? new ServiceCollection().BuildServiceProvider());
    }

    public static Task<IResult> ProbeHandleRemoveRangeAsync(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        IEnumerable<TModel> model,
        bool? cacheable)
        => handler.HandleRemoveRangeAsync(model, cacheable);

    public static Task<IResult> ProbeHandleUpdateAsync(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        TKey id,
        TModel model,
        bool? cacheable)
        => handler.HandleUpdateAsync(id, model, cacheable);

    public static Task<KyrolusBatchOperationResult<TResponse, TKey>> ProbeExecuteBatchPatchAsync(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        KyrolusBatchOperation<TModel, TKey> operation,
        bool returnData,
        CancellationToken cancellationToken)
        => (Task<KyrolusBatchOperationResult<TResponse, TKey>>)HandlerType
            .GetMethod("ExecuteBatchPatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, [operation, returnData, cancellationToken])!;

    public static bool ProbeIsConcurrencyException(Exception ex)
        => (bool)HandlerType
            .GetMethod("IsConcurrencyException", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [ex])!;

    public static bool ProbeTryEnsureContextMatch(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        TResponse entity,
        string? propertyName,
        string? rawValue,
        string label,
        out IResult? errorResult)
    {
        var args = new object?[] { entity, propertyName, rawValue, label, null };
        var success = (bool)HandlerType
            .GetMethod("TryEnsureContextMatch", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, args)!;
        errorResult = (IResult?)args[4];
        return success;
    }

    public static bool ProbeTryApplyIfMatch(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        TResponse entity,
        out IResult? errorResult)
    {
        var args = new object?[] { entity, null };
        var success = (bool)HandlerType
            .GetMethod("TryApplyIfMatch", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, args)!;
        errorResult = (IResult?)args[1];
        return success;
    }

    public static bool ProbeTryBuildOrder(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        string? orderBy,
        out IResult? errorResult)
    {
        var args = new object?[] { orderBy, null, null };
        var success = (bool)HandlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => m.Name == "TryBuildOrder" && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(handler, args)!;
        errorResult = (IResult?)args[2];
        return success;
    }

    public static bool ProbeTryBuildOrder(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        IReadOnlyList<OrderClause>? clauses,
        out IResult? errorResult)
    {
        var args = new object?[] { clauses, null, null };
        var success = (bool)HandlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => m.Name == "TryBuildOrder" && m.GetParameters()[0].ParameterType == typeof(IReadOnlyList<OrderClause>))
            .Invoke(handler, args)!;
        errorResult = (IResult?)args[2];
        return success;
    }

    public static bool ProbeTryBuildIncludeGraph(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        EndpointNames endpoint,
        object? includeGraph,
        IReadOnlyCollection<string>? allowedIncludes,
        out IncludeGraph<TResponse>? graph,
        out IResult? errorResult)
    {
        var args = new object?[] { endpoint, includeGraph, allowedIncludes, null, null };
        var success = (bool)HandlerType
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => m.Name == "TryBuildIncludeGraph" && m.GetParameters()[1].ParameterType == typeof(object))
            .Invoke(handler, args)!;
        graph = (IncludeGraph<TResponse>?)args[3];
        errorResult = (IResult?)args[4];
        return success;
    }

    public static bool ProbeTrySetEntityId(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        TResponse entity,
        TKey id,
        out IResult? errorResult)
    {
        var args = new object?[] { entity, id, null };
        var success = (bool)HandlerType
            .GetMethod("TrySetEntityId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, args)!;
        errorResult = (IResult?)args[2];
        return success;
    }

    public static bool ProbeTrySetCompositeKey(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        TResponse entity,
        object?[] keyValues,
        out IResult? errorResult)
    {
        var args = new object?[] { entity, keyValues, null };
        var success = (bool)HandlerType
            .GetMethod("TrySetCompositeKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, args)!;
        errorResult = (IResult?)args[2];
        return success;
    }

    public static bool ProbeTryParseEtagValue(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        string raw,
        Type targetType,
        out object? value)
    {
        var args = new object?[] { raw, targetType, null };
        var success = (bool)HandlerType
            .GetMethod("TryParseEtagValue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, args)!;
        value = args[2];
        return success;
    }

    public static string ProbeNormalizeEtagValue(object value)
        => (string)HandlerType
            .GetMethod("NormalizeEtagValue", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [value])!;

    public static bool ProbeTrySetPropertyValue(object target, string propertyName, object? value)
        => (bool)HandlerType
            .GetMethod("TrySetPropertyValue", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [target, propertyName, value])!;

    public static IReadOnlyCollection<string>? ProbeMergeAllowlist(
        IReadOnlyCollection<string>? baseAllowed,
        IReadOnlyCollection<string>? extraAllowed)
        => (IReadOnlyCollection<string>?)HandlerType
            .GetMethod("MergeAllowlist", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [baseAllowed, extraAllowed]);

    public static IReadOnlyList<string>? ProbeExtractIncludeGraphPaths(object? includeGraph)
        => (IReadOnlyList<string>?)HandlerType
            .GetMethod("ExtractIncludeGraphPaths", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [includeGraph]);

    public static bool ProbeTryConvertKey(string raw, Type targetType, out object? value)
    {
        var args = new object?[] { raw, targetType, null };
        var success = (bool)HandlerType
            .GetMethod("TryConvertKey", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
        value = args[2];
        return success;
    }

    public static bool ProbeTryGetPropertyValue(TResponse entity, string propertyName, out object? value)
    {
        var args = new object?[] { entity, propertyName, null };
        var success = (bool)HandlerType
            .GetMethod("TryGetPropertyValue", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
        value = args[2];
        return success;
    }

    public static object ProbeMapPagedResult(KyrolusPagedResult<TResponse> paged, Type viewModelType)
        => HandlerType
            .GetMethod("MapPagedResult", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [paged, viewModelType])!;

    public static Type ProbeResolveViewModelType(DefaultCommandQueryHandler<TResponse, TModel, TKey> handler, EndpointNames endpoint)
        => (Type)HandlerType
            .GetMethod("ResolveViewModelType", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, [endpoint])!;

    public static IResult ProbeBuildErrorResult(
        DefaultCommandQueryHandler<TResponse, TModel, TKey> handler,
        int statusCode,
        string code,
        string title,
        IReadOnlyList<KyrolusErrorItem>? errors = null)
        => (IResult)HandlerType
            .GetMethod("BuildErrorResult", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(handler, [statusCode, code, title, errors])!;

    public static Exception ProbeCreateNamedException(string fullName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"RuntimeDynamicExceptions_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var typeBuilder = module.DefineType(fullName, TypeAttributes.Class | TypeAttributes.Public, typeof(Exception));
        var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(Exception).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Ret);
        var type = typeBuilder.CreateType() ?? throw new InvalidOperationException($"Could not create exception type '{fullName}'.");
        return (Exception)Activator.CreateInstance(type)!;
    }
}

internal static class RuntimeFilterBuilderProbe
{
    private static readonly Type BuilderType = typeof(KyrolusSous.EndpointKit.Marten.FilterBuilder);

    public static bool ProbeTryConvert(string? raw, Type targetType, out object? value)
    {
        var args = new object?[] { raw, targetType, null };
        var success = (bool)BuilderType
            .GetMethod("TryConvert", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
        value = args[2];
        return success;
    }
}

internal sealed class RuntimeEndpointKitMartenProbeItem
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int? OptionalCount { get; set; }
    public RuntimeEndpointKitMartenProbeChild? Child { get; set; }
}

internal sealed class RuntimeEndpointKitMartenProbeChild
{
    public string Name { get; set; } = string.Empty;
    public RuntimeEndpointKitMartenProbeGrandChild? GrandChild { get; set; }
}

internal sealed class RuntimeEndpointKitMartenProbeGrandChild
{
    public string Label { get; set; } = string.Empty;
}

internal sealed class RuntimeReadOnlyVersionProbeItem
{
    public string ReadOnlyVersion { get; } = "probe-version";
}

internal sealed class RuntimeEmptyPatchPayload;

internal sealed class RuntimeAssignedIdProbeItem
{
    public Guid AssignedId { get; set; }
}

internal sealed class RuntimeNullableEnumProbeItem
{
    public RuntimeSeekProbeStatus? Status { get; init; }
}


