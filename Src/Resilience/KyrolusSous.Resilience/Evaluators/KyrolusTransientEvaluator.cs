namespace KyrolusSous.Resilience;

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
