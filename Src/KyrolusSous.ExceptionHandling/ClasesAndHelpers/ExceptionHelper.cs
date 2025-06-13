global using KyrolusSous.ExceptionHandling.ClasesAndHelpers;
using System.Text.Json;

namespace KyrolusSous.ExceptionHandling.ClasesAndHelpers;

public static class ExceptionHelper
{
    public static void LogError(ILogger logger, ErrorContextInfo contextInfo, Exception ex, HttpStatusCode code)
    {
        logger.LogError(
            """
                Error occurred in {Controller}/{Action}
                Request Path: {RequestPath},
                Time of occurrence: {Time}
                HTTP Method: {HttpMethod}, 
                Error Message: {ExceptionMessage},
                Error StackTree: {ExceptionStackTrace},
                HttpStatusCode: {HttpStatusCode}
             """,
            contextInfo.Controller, contextInfo.Action, contextInfo.RequestPath, DateTime.UtcNow, contextInfo.HttpMethod,
            ex.Message, ex.StackTrace, code);
    }

    public static Task ReturnErrorResponse(ILogger logger, HttpContext context, ErrorContextInfo contextInfo, Exception ex, HttpStatusCode code, string message)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        LogError(logger, contextInfo, ex, code);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        Console.WriteLine($"StatusCode: {code}");
        return context.Response.WriteAsync(JsonSerializer.Serialize(new Response((int)code,
                    message, false, data: null, new ExceptionResponse(context, contextInfo, ex)), jsonOptions));
    }


}

