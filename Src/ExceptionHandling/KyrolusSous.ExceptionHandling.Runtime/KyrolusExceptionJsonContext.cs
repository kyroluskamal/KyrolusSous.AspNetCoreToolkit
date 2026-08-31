namespace KyrolusSous.ExceptionHandling.Runtime;

[JsonSerializable(typeof(KyrolusErrorEnvelope))]
[JsonSerializable(typeof(KyrolusErrorItem))]
[JsonSerializable(typeof(IReadOnlyList<KyrolusErrorItem>))]
[JsonSerializable(typeof(List<KyrolusErrorItem>))]
[JsonSerializable(typeof(KyrolusErrorContextInfo))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(TimeOnly))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(Uri))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class KyrolusExceptionJsonContext : JsonSerializerContext
{
}
