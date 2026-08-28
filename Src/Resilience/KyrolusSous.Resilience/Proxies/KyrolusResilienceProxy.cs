using System.Reflection;

namespace KyrolusSous.Resilience;

/// <summary>
/// Dynamic dispatch proxy that intercepts all method invocations on an interface and wraps them in a named resilience pipeline.
/// Hardened to unwrap TargetInvocationException for proper Polly transient evaluation.
/// </summary>
public class KyrolusResilienceProxy<TInterface> : DispatchProxy where TInterface : class
{
    private TInterface _target = default!;
    private IKyrolusResiliencePipelineProvider _pipelineProvider = default!;
    private string _pipelineName = "default";

    public static TInterface Create(
        TInterface target,
        IKyrolusResiliencePipelineProvider pipelineProvider,
        string pipelineName = "default")
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pipelineProvider);

        var proxy = Create<TInterface, KyrolusResilienceProxy<TInterface>>();
        var typedProxy = (KyrolusResilienceProxy<TInterface>)(object)proxy;
        typedProxy._target = target;
        typedProxy._pipelineProvider = pipelineProvider;
        typedProxy._pipelineName = pipelineName;

        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) return null;

        var returnType = targetMethod.ReturnType;

        // Task<T>
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var method = typeof(KyrolusResilienceProxy<TInterface>)
                .GetMethod(nameof(InvokeAsyncTask), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

            return method.Invoke(this, [targetMethod, args]);
        }

        // Task
        if (returnType == typeof(Task))
        {
            return InvokeAsyncVoid(targetMethod, args);
        }

        // ValueTask<T>
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var method = typeof(KyrolusResilienceProxy<TInterface>)
                .GetMethod(nameof(InvokeAsyncValueTask), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

            return method.Invoke(this, [targetMethod, args]);
        }

        // Synchronous invocation
        var pipeline = _pipelineProvider.GetPipeline(_pipelineName);
        return pipeline.Execute(() =>
        {
            try
            {
                return targetMethod.Invoke(_target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        });
    }

    private async Task<TResult> InvokeAsyncTask<TResult>(MethodInfo method, object?[]? args)
    {
        var pipeline = _pipelineProvider.GetPipeline<TResult>(_pipelineName);
        return await pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                var task = (Task<TResult>)method.Invoke(_target, args)!;
                return await task;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        });
    }

    private async Task InvokeAsyncVoid(MethodInfo method, object?[]? args)
    {
        var pipeline = _pipelineProvider.GetPipeline(_pipelineName);
        await pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                var task = (Task)method.Invoke(_target, args)!;
                await task;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        });
    }

    private async ValueTask<TResult> InvokeAsyncValueTask<TResult>(MethodInfo method, object?[]? args)
    {
        var pipeline = _pipelineProvider.GetPipeline<TResult>(_pipelineName);
        return await pipeline.ExecuteAsync(async ct =>
        {
            try
            {
                var valueTask = (ValueTask<TResult>)method.Invoke(_target, args)!;
                return await valueTask;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        });
    }
}
