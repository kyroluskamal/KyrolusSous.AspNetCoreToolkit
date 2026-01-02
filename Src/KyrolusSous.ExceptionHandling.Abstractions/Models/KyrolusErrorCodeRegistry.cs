
namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public static partial class KyrolusErrorCodeRegistry
{
    private static readonly ConcurrentDictionary<string, KyrolusErrorCodeDefinition> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex CodePattern = MyRegex();

    static KyrolusErrorCodeRegistry()
    {
        RegisterCoreDefaults();
    }

    public static void Register(KyrolusErrorCodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Code))
        {
            throw new KyrolusErrorCodeRegistryException("Error code cannot be empty.");
        }

        if (!CodePattern.IsMatch(definition.Code))
        {
            throw new KyrolusErrorCodeRegistryException($"Error code '{definition.Code}' does not match the naming convention.");
        }

        if (!Registry.TryAdd(definition.Code, definition))
        {
            throw new KyrolusErrorCodeRegistryException($"Error code '{definition.Code}' is already registered.");
        }
    }

    public static void RegisterRange(IEnumerable<KyrolusErrorCodeDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            Register(definition);
        }
    }

    public static bool TryGet(string code, out KyrolusErrorCodeDefinition definition)
    {
        return Registry.TryGetValue(code, out definition!);
    }

    public static IReadOnlyCollection<KyrolusErrorCodeDefinition> Snapshot()
    {
        return Registry.Values.ToArray();
    }

    public static bool IsValidCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code) && CodePattern.IsMatch(code);
    }

    private static void RegisterCoreDefaults()
    {
        RegisterRange(
        [
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Validation, "Validation failed", HttpStatusCode.BadRequest),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.NotFound, "Not found", HttpStatusCode.NotFound),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Conflict, "Conflict", HttpStatusCode.Conflict),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Unauthorized, "Unauthorized", HttpStatusCode.Unauthorized),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Forbidden, "Forbidden", HttpStatusCode.Forbidden),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Timeout, "Timeout", HttpStatusCode.GatewayTimeout),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.ExternalService, "External service error", HttpStatusCode.BadGateway),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.RateLimit, "Rate limit exceeded", (HttpStatusCode)429),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.BadRequest, "Bad request", HttpStatusCode.BadRequest),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.InvalidJson, "Invalid JSON", HttpStatusCode.BadRequest),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.DatabaseError, "Database error", HttpStatusCode.InternalServerError),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.ConcurrencyConflict, "Concurrency conflict", HttpStatusCode.Conflict),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.InternalError, "Internal server error", HttpStatusCode.InternalServerError),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Cancelled, "Request cancelled", HttpStatusCode.RequestTimeout)
        ]);
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();

}
