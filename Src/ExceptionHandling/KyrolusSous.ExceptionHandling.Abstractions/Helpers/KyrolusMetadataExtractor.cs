
namespace KyrolusSous.ExceptionHandling.Abstractions.Helpers;

public static class KyrolusMetadataExtractor
{
    public static Dictionary<string, object?>? Extract(
        Exception exception,
        IReadOnlyDictionary<string, object?>? explicitMetadata = null)
    {
        var dict = explicitMetadata is { Count: > 0 }
            ? new Dictionary<string, object?>(explicitMetadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        ExtractExceptionData(exception, dict);
        ExtractSpecificMetadata(exception, dict);

        return dict.Count > 0 ? dict : null;
    }

    private static void ExtractExceptionData(Exception exception, Dictionary<string, object?> dict)
    {
        if (exception.Data.Count == 0)
        {
            return;
        }

        foreach (var key in exception.Data.Keys)
        {
            if (key is not null)
            {
                dict[key.ToString()!] = exception.Data[key];
            }
        }
    }

    private static void ExtractSpecificMetadata(Exception exception, Dictionary<string, object?> dict)
    {
        switch (exception)
        {
            case CultureNotFoundException cultureEx:
                if (!string.IsNullOrWhiteSpace(cultureEx.InvalidCultureName))
                {
                    dict["invalidCultureName"] = cultureEx.InvalidCultureName;
                }
                if (!string.IsNullOrWhiteSpace(cultureEx.ParamName))
                {
                    dict["paramName"] = cultureEx.ParamName;
                }
                break;

            case ArgumentException argEx when !string.IsNullOrWhiteSpace(argEx.ParamName):
                dict["paramName"] = argEx.ParamName;
                break;

            case SocketException sockEx:
                dict["socketErrorCode"] = sockEx.SocketErrorCode.ToString();
                break;

            case HttpRequestException httpEx when httpEx.StatusCode.HasValue:
                dict["httpStatusCode"] = (int)httpEx.StatusCode.Value;
                break;
        }
    }
}
