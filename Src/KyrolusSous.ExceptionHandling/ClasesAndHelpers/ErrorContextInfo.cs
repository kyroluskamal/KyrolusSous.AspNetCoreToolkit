namespace KyrolusSous.ExceptionHandling.ClasesAndHelpers;
// [JsonSerializable(typeof(ErrorContextInfo))]
// public partial class ErrorContextInfoContext : JsonSerializerContext { }
public class ErrorContextInfo(HttpContext context)
{
    public string RequestPath { get; set; } = context?.Request.Path!;
    public string HttpMethod { get; set; } = context?.Request.Method!;
    public string? Controller { get; set; } = context?.GetRouteData()?.Values["controller"]?.ToString();
    public string? Action { get; set; } = context?.GetRouteData()?.Values["action"]?.ToString();
}
