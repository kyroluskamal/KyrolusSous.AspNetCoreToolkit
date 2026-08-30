namespace KyrolusSous.ExceptionHandling.Abstractions.Helpers;

/// <summary>
/// Utility helper for extracting structured diagnostic metadata from various .NET and custom exception types.
/// </summary>
/// <remarks>
/// Automatically inspects <see cref="Exception.Data"/>, <see cref="Interfaces.IKyrolusExceptionWithMetadata"/>,
/// and common .NET BCL exceptions (e.g. <see cref="ArgumentException.ParamName"/>, <see cref="JsonException.LineNumber"/>, <see cref="SocketException.SocketErrorCode"/>).
/// </remarks>
public static class KyrolusMetadataExtractor
{
    /// <summary>
    /// Extracts a combined dictionary of diagnostic metadata from the exception and explicit metadata dictionary.
    /// </summary>
    /// <param name="exception">The exception to extract metadata from.</param>
    /// <param name="explicitMetadata">Optional explicit metadata to merge.</param>
    /// <returns>A dictionary of extracted metadata, or <c>null</c> if no metadata was found.</returns>
    public static Dictionary<string, object?>? Extract(
        Exception exception,
        IReadOnlyDictionary<string, object?>? explicitMetadata = null)
    {
        var dict = explicitMetadata is { Count: > 0 }
            ? new Dictionary<string, object?>(explicitMetadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        ExtractExceptionData(exception, dict);
        ExtractCustomMetadata(exception, dict);
        ExtractSpecificMetadata(exception, dict);

        return dict.Count > 0 ? dict : null;
    }

    private static void ExtractExceptionData(Exception exception, Dictionary<string, object?> dict)
    {
        if (exception.Data.Count == 0)
            return;

        foreach (var key in exception.Data.Keys)
            if (key is not null) dict[key.ToString()!] = exception.Data[key];
    }

    private static void ExtractCustomMetadata(Exception exception, Dictionary<string, object?> dict)
    {
        if (exception is IKyrolusExceptionWithMetadata metadataEx)
        {
            var customMetadata = metadataEx.GetMetadata();
            if (customMetadata is { Count: > 0 })
                foreach (var (key, value) in customMetadata)
                    dict[key] = value;
        }
    }

    private static void ExtractSpecificMetadata(Exception exception, Dictionary<string, object?> dict)
    {
        ExtractArgumentMetadata(exception, dict);
        ExtractNetworkMetadata(exception, dict);
        ExtractJsonAndIoMetadata(exception, dict);
    }

    private static void ExtractArgumentMetadata(Exception exception, Dictionary<string, object?> dict)
    {
        switch (exception)
        {
            case CultureNotFoundException cultureEx:
                if (!string.IsNullOrWhiteSpace(cultureEx.InvalidCultureName))
                    dict["invalidCultureName"] = cultureEx.InvalidCultureName;
                if (!string.IsNullOrWhiteSpace(cultureEx.ParamName))
                    dict["paramName"] = cultureEx.ParamName;
                break;

            case ArgumentOutOfRangeException outOfRangeEx:
                if (!string.IsNullOrWhiteSpace(outOfRangeEx.ParamName))
                    dict["paramName"] = outOfRangeEx.ParamName;
                if (outOfRangeEx.ActualValue is not null)
                    dict["actualValue"] = outOfRangeEx.ActualValue;
                break;

            case ArgumentException argEx when !string.IsNullOrWhiteSpace(argEx.ParamName):
                dict["paramName"] = argEx.ParamName;
                break;
        }
    }

    private static void ExtractNetworkMetadata(Exception exception, Dictionary<string, object?> dict)
    {
        switch (exception)
        {
            case SocketException sockEx:
                dict["socketErrorCode"] = sockEx.SocketErrorCode.ToString();
                dict["nativeErrorCode"] = sockEx.NativeErrorCode;
                break;

            case HttpRequestException httpEx:
                if (httpEx.StatusCode.HasValue)
                    dict["httpStatusCode"] = (int)httpEx.StatusCode.Value;
                dict["httpRequestError"] = httpEx.HttpRequestError.ToString();
                break;
        }
    }

    private static void ExtractJsonAndIoMetadata(Exception exception, Dictionary<string, object?> dict)
    {
        switch (exception)
        {
            case JsonException jsonEx:
                if (jsonEx.LineNumber.HasValue)
                    dict["lineNumber"] = jsonEx.LineNumber.Value;
                if (jsonEx.BytePositionInLine.HasValue)
                    dict["bytePositionInLine"] = jsonEx.BytePositionInLine.Value;
                if (!string.IsNullOrWhiteSpace(jsonEx.Path))
                    dict["jsonPath"] = jsonEx.Path;
                break;

            case FileNotFoundException fileEx when !string.IsNullOrWhiteSpace(fileEx.FileName):
                dict["fileName"] = fileEx.FileName;
                break;

            case DirectoryNotFoundException dirEx:
                dict["message"] = dirEx.Message;
                break;
        }
    }
}
