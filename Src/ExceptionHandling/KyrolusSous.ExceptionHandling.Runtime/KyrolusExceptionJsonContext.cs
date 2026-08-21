namespace KyrolusSous.ExceptionHandling.Runtime;

[JsonSerializable(typeof(KyrolusErrorEnvelope))]
[JsonSerializable(typeof(KyrolusErrorItem))]
[JsonSerializable(typeof(ErrorContextInfo))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, object?>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class KyrolusExceptionJsonContext : JsonSerializerContext
{
}
