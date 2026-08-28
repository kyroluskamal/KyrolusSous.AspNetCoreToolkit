using System.Net.Sockets;
using KyrolusSous.ExceptionHandling.Abstractions;

namespace KyrolusSous.Resilience;

/// <summary>
/// Default implementation of <see cref="IKyrolusTransientExceptionEvaluator"/> checking common network, HTTP, socket, and Kyrolus toolkit transient errors.
/// </summary>
public class KyrolusDefaultTransientExceptionEvaluator : IKyrolusTransientExceptionEvaluator
{
    public bool IsTransient(Exception exception) => KyrolusTransientEvaluator.IsTransient(exception);
}

/// <summary>
/// Composite transient evaluator that delegates to all registered <see cref="IKyrolusTransientExceptionEvaluator"/> instances.
/// </summary>
public class KyrolusCompositeTransientEvaluator(IEnumerable<IKyrolusTransientExceptionEvaluator> evaluators) : IKyrolusTransientExceptionEvaluator
{
    private readonly IReadOnlyList<IKyrolusTransientExceptionEvaluator> _evaluators = evaluators.ToList();

    public bool IsTransient(Exception exception)
    {
        if (exception is null) return false;

        for (var i = 0; i < _evaluators.Count; i++)
        {
            if (_evaluators[i].IsTransient(exception))
            {
                return true;
            }
        }

        return KyrolusTransientEvaluator.IsTransient(exception);
    }
}

/// <summary>
/// Static helper for transient exception detection.
/// </summary>
public static class KyrolusTransientEvaluator
{
    public static bool IsTransient(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is KyrolusException kyrolusException)
        {
            return kyrolusException.IsTransient;
        }

        if (exception is TimeoutException or SocketException)
        {
            return true;
        }

        if (exception is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode.HasValue)
            {
                var code = (int)httpEx.StatusCode.Value;
                return code is 408 or 429 or (>= 500 and <= 599);
            }
            return true;
        }

        if (exception is TaskCanceledException or OperationCanceledException)
        {
            return false;
        }

        if (exception.InnerException is not null)
        {
            return IsTransient(exception.InnerException);
        }

        return false;
    }

    public static bool IsTransientHttp(HttpResponseMessage? response, Exception? exception)
    {
        if (exception is not null)
        {
            return IsTransient(exception);
        }

        if (response is not null)
        {
            var code = (int)response.StatusCode;
            return code is 408 or 429 or (>= 500 and <= 599);
        }

        return false;
    }
}
