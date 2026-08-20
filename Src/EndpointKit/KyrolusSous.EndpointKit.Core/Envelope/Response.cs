namespace KyrolusSous.EndpointKit.Core.Envelope;

public class Response<T>
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }

    public Response() { }

    public Response(int statusCode, string message, bool isSuccess, T? data = default)
    {
        StatusCode = statusCode;
        Message = message;
        IsSuccess = isSuccess;
        Data = data;
    }

    public static Response<T> Success(T data, string message = "Operation completed successfully", int statusCode = 200)
        => new(statusCode, message, true, data);

    public static Response<T> Failure(string message, int statusCode = 400)
        => new(statusCode, message, false, default);
}

public class Response : Response<object>
{
    public Response() { }

    public Response(int statusCode, string message, bool isSuccess, object? data = null)
        : base(statusCode, message, isSuccess, data) { }
}
