using System.Text.Json.Serialization;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.CachingAbstractionsIntegrationTests;

[JsonSerializable(typeof(CachingProbePayload))]
public partial class CachingProbeJsonContext : JsonSerializerContext
{
}

public sealed record CachingProbePayload(string Name, int Count);
