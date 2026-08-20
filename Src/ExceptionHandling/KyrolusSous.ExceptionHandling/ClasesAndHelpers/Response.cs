namespace KyrolusSous.ExceptionHandling.ClasesAndHelpers;

public class Response
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public object? Data { get; set; }

    public Response() { }

    public Response(int statusCode, string message, bool isSuccess, object? data = null)
    {
        StatusCode = statusCode;
        Message = message;
        IsSuccess = isSuccess;
        Data = data;
    }
}
