namespace KyrolusSous.ExceptionHandling.ClasesAndHelpers;
// [JsonSerializable(typeof(ExceptionResponse))]
// public partial class ExceptionResponseContext : JsonSerializerContext { }
public class ExceptionResponse
{
    public class LocationStacks
    {
        public string ClassName { get; set; } = string.Empty;
        public string CausingFunction { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int LineNumber { get; set; }
    }
    public ExceptionResponse(HttpContext context, ErrorContextInfo contextInfo, Exception exception)
    {
        exception ??= new Exception();
        contextInfo ??= new ErrorContextInfo(context);


        var stackTrace = new StackTrace(exception, true);
        StackFrame? targetFrame = null;
        var locationsStack = new List<LocationStacks>();
        // البحث عن أول فريم يحتوي على ملف فعلي ورقم سطر
        foreach (var frame in stackTrace.GetFrames())
        {
            var fileName = frame.GetFileName();
            var lineNumber = frame.GetFileLineNumber();

            if (!string.IsNullOrEmpty(fileName) && lineNumber > 0)
            {
                targetFrame = frame;
                locationsStack.Add(new LocationStacks
                {
                    ClassName = frame.GetMethod()?.DeclaringType?.Name ?? "UnknownClass",
                    CausingFunction = frame.GetMethod()?.Name ?? "UnknownMethod",
                    FileName = fileName,
                    LineNumber = lineNumber
                });
            }
        }


        ExceptionType = exception.GetType().Name;
        Controller = contextInfo.Controller;
        Action = contextInfo.Action;
        Path = contextInfo.RequestPath;
        Method = contextInfo.HttpMethod;
        ErrorDetails = exception is ValidationException valex
                            ? valex.Errors.ToList()
                            : exception.Message;
        TimeOccurred = DateTime.UtcNow;
        TraceId = context?.TraceIdentifier!;
        ClassName = targetFrame?.GetMethod()?.DeclaringType?.Name ?? "UnknownClass";
        CausingFunction = targetFrame?.GetMethod()?.Name ?? "UnknownMethod";
        FileName = targetFrame?.GetFileName() ?? "UnknownFile";
        LineNumber = targetFrame?.GetFileLineNumber() ?? -1;
        LocationsStacks = locationsStack;
    }

    public string ExceptionType { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string Path { get; set; }
    public string Method { get; set; }
    public dynamic ErrorDetails { get; set; }
    public DateTime TimeOccurred { get; set; }
    public string TraceId { get; set; }
    public string ClassName { get; set; }
    public string CausingFunction { get; set; }
    public string FileName { get; set; }
    public int LineNumber { get; set; }
    public List<LocationStacks> LocationsStacks { get; set; }
}