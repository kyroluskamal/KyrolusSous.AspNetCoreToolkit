namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public static partial class KyrolusErrorCodeRegistry
{
    private static readonly ConcurrentDictionary<string, KyrolusErrorCodeDefinition> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Lock ConfigLock = new();
    private static string? _configuredBy;
    private static Regex? _customPattern;
    private static Func<string, bool>? _customValidator;
    private static bool _validationDisabled;

    static KyrolusErrorCodeRegistry()
    {
        RegisterCoreDefaults();
    }

    public static bool IsConfigured => _configuredBy is not null;

    public static string? ConfiguredMethod => _configuredBy;

    public static void SetCodePattern(string regexPattern, RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regexPattern);
        EnsureNotConfigured(nameof(SetCodePattern));

        _customPattern = new Regex(regexPattern, options);
        _customValidator = null;
        _validationDisabled = false;
    }

    public static void SetCodePattern(Regex regex)
    {
        ArgumentNullException.ThrowIfNull(regex);
        EnsureNotConfigured(nameof(SetCodePattern));

        _customPattern = regex;
        _customValidator = null;
        _validationDisabled = false;
    }

    public static void SetCustomValidator(Func<string, bool> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        EnsureNotConfigured(nameof(SetCustomValidator));

        _customValidator = validator;
        _customPattern = null;
        _validationDisabled = false;
    }

    public static void DisableValidation()
    {
        EnsureNotConfigured(nameof(DisableValidation));

        _validationDisabled = true;
        _customPattern = null;
        _customValidator = null;
    }

    public static void ResetToDefault()
    {
        lock (ConfigLock)
        {
            _validationDisabled = false;
            _customPattern = null;
            _customValidator = null;
            _configuredBy = null;
        }
    }

    public static void Register(KyrolusErrorCodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Code))
            throw new KyrolusErrorCodeRegistryException("Error code cannot be empty.");

        if (!IsValidCode(definition.Code))
            throw new KyrolusErrorCodeRegistryException($"Error code '{definition.Code}' does not match the naming convention.");

        if (!Registry.TryAdd(definition.Code, definition))
            throw new KyrolusErrorCodeRegistryException($"Error code '{definition.Code}' is already registered.");
    }

    public static void RegisterRange(IEnumerable<KyrolusErrorCodeDefinition> definitions)
    {
        foreach (var definition in definitions)
            Register(definition);
    }

    public static bool TryGet(string code, out KyrolusErrorCodeDefinition definition)
        => Registry.TryGetValue(code, out definition!);

    public static IReadOnlyCollection<KyrolusErrorCodeDefinition> Snapshot()
        => [.. Registry.Values];

    public static bool IsValidCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        if (_validationDisabled)
            return true;

        if (_customValidator is not null)
            return _customValidator(code);

        if (_customPattern is not null)
            return _customPattern.IsMatch(code);

        return DefaultSnakeCaseRegex().IsMatch(code);
    }

    private static void EnsureNotConfigured(string callerMethod)
    {
        lock (ConfigLock)
        {
            if (_configuredBy is not null)
            {
                throw new KyrolusErrorCodeRegistryException(
                    $"Cannot configure error code naming convention via '{callerMethod}'. " +
                    $"It has already been configured via '{_configuredBy}'. " +
                    "Multiple conflicting configurations are not allowed in the same application.");
            }

            _configuredBy = callerMethod;
        }
    }

    private static void RegisterCoreDefaults()
    {
        RegisterRange(
        [
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Validation, "Validation failed", HttpStatusCode.BadRequest, ShouldLog: false),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.NotFound, "Not found", HttpStatusCode.NotFound, ShouldLog: false),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Conflict, "Conflict", HttpStatusCode.Conflict, ShouldLog: false),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Unauthorized, "Unauthorized", HttpStatusCode.Unauthorized, ShouldLog: false),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Forbidden, "Forbidden", HttpStatusCode.Forbidden, ShouldLog: false),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Timeout, "Timeout", HttpStatusCode.GatewayTimeout, IsTransient: true, ShouldLog: true),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.ExternalService, "External service error", HttpStatusCode.BadGateway, IsTransient: true, ShouldLog: true),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.RateLimit, "Rate limit exceeded", (HttpStatusCode)429, IsTransient: true, ShouldLog: false),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.BadRequest, "Bad request", HttpStatusCode.BadRequest, ShouldLog: false),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.InvalidJson, "Invalid JSON", HttpStatusCode.BadRequest, ShouldLog: false),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.DatabaseError, "Database error", HttpStatusCode.InternalServerError, IsTransient: true, ShouldLog: true),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.ConcurrencyConflict, "Concurrency conflict", HttpStatusCode.Conflict, IsTransient: true, ShouldLog: true),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.InternalError, "Internal server error", HttpStatusCode.InternalServerError, IsTransient: false, ShouldLog: true),
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.Cancelled, "Request cancelled", HttpStatusCode.RequestTimeout, IsTransient: true, ShouldLog: false)
        ]);
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex DefaultSnakeCaseRegex();
}
