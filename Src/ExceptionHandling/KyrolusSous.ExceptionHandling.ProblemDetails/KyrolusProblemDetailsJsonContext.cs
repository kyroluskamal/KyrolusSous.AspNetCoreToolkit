namespace KyrolusSous.ExceptionHandling.ProblemDetails;

[JsonSerializable(typeof(MvcProblemDetails))]
[JsonSerializable(typeof(KyrolusErrorItem))]
[JsonSerializable(typeof(IReadOnlyList<KyrolusErrorItem>))]
[JsonSerializable(typeof(List<KyrolusErrorItem>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, object?>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(object))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class KyrolusProblemDetailsJsonContext : JsonSerializerContext
{
}
