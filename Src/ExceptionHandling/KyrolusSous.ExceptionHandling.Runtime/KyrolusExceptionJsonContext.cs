namespace KyrolusSous.ExceptionHandling.Runtime;

[JsonSerializable(typeof(KyrolusErrorEnvelope))]
[JsonSerializable(typeof(KyrolusErrorItem))]
[JsonSerializable(typeof(ErrorContextInfo))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class KyrolusExceptionJsonContext : JsonSerializerContext
{
}
