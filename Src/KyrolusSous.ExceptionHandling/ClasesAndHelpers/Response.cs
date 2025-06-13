namespace KyrolusSous.ExceptionHandling.ClasesAndHelpers;

// [JsonSerializable(typeof(Response))]
// public partial class ResponseContext : JsonSerializerContext { }

public class Response(int code, string message, bool isSuccess = true, object? data = null, ExceptionResponse? exception = null)
{
    public int StatusCode { get; set; } = code;

    public string Message { get; set; } = message;
    public bool IsSuccess { get; set; } = isSuccess;
    public object? Data { get; set; } = data;
    public ExceptionResponse? Exception { get; set; } = exception;
}
