namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Thread-safe central registry for managing and enforcing domain error codes, titles, HTTP status codes, and naming conventions.
/// </summary>
/// <remarks>
/// Use this registry during application bootstrap to standardize error codes across development teams.
/// Supports regex naming patterns, custom validators, strict-mode enforcement in Development, and overrides.
/// </remarks>
/// <example>
/// <code>
/// // Registering a domain error code at startup:
/// KyrolusErrorCodeRegistry.Register(new KyrolusErrorCodeDefinition(
///     Code: "insufficient_funds",
///     Title: "Insufficient Funds",
///     StatusCode: HttpStatusCode.UnprocessableEntity,
///     ShouldLog: false));
/// </code>
/// </example>
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

    /// <summary>
    /// Gets or sets a value indicating whether strict mode is enabled.
    /// When enabled, attempting to instantiate a <see cref="KyrolusDomainException"/> with an unregistered code throws a <see cref="KyrolusErrorCodeRegistryException"/>.
    /// </summary>
    public static bool StrictMode { get; set; }

    /// <summary>
    /// Enables strict mode for error code enforcement (recommended for Development and Test environments).
    /// </summary>
    public static void EnableStrictMode() => StrictMode = true;

    /// <summary>
    /// Disables strict mode for error code enforcement (allowing safe fallbacks in Production).
    /// </summary>
    public static void DisableStrictMode() => StrictMode = false;

    /// <summary>
    /// Gets a value indicating whether the error code validation rule has already been configured.
    /// </summary>
    public static bool IsConfigured => _configuredBy is not null;

    /// <summary>
    /// Gets the name of the method that configured the naming convention rule.
    /// </summary>
    public static string? ConfiguredMethod => _configuredBy;

    /// <summary>
    /// Configures a custom regular expression string pattern that all registered error codes must match.
    /// </summary>
    /// <param name="regexPattern">The regular expression pattern string (e.g. <c>"^[A-Z]{3}_[0-9]{3}$"</c>).</param>
    /// <param name="options">Regex compilation options.</param>
    public static void SetCodePattern(string regexPattern, RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regexPattern);
        EnsureNotConfigured(nameof(SetCodePattern));

        _customPattern = new Regex(regexPattern, options);
        _customValidator = null;
        _validationDisabled = false;
    }

    /// <summary>
    /// Configures a compiled <see cref="Regex"/> object that all registered error codes must match.
    /// </summary>
    /// <param name="regex">The compiled regular expression.</param>
    public static void SetCodePattern(Regex regex)
    {
        ArgumentNullException.ThrowIfNull(regex);
        EnsureNotConfigured(nameof(SetCodePattern));

        _customPattern = regex;
        _customValidator = null;
        _validationDisabled = false;
    }

    /// <summary>
    /// Configures a custom delegate validator function to validate error code strings.
    /// </summary>
    /// <param name="validator">The validation predicate function.</param>
    public static void SetCustomValidator(Func<string, bool> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        EnsureNotConfigured(nameof(SetCustomValidator));

        _customValidator = validator;
        _customPattern = null;
        _validationDisabled = false;
    }

    /// <summary>
    /// Disables error code pattern validation entirely, allowing any string format.
    /// </summary>
    public static void DisableValidation()
    {
        EnsureNotConfigured(nameof(DisableValidation));

        _validationDisabled = true;
        _customPattern = null;
        _customValidator = null;
    }

    /// <summary>
    /// Resets the registry configuration, pattern validators, strict mode, and restores the 14 default core error codes.
    /// </summary>
    public static void ResetToDefault()
    {
        lock (ConfigLock)
        {
            _validationDisabled = false;
            _customPattern = null;
            _customValidator = null;
            _configuredBy = null;
            StrictMode = false;
            Registry.Clear();
            RegisterCoreDefaults();
        }
    }

    /// <summary>
    /// Clears all definitions from the registry.
    /// </summary>
    public static void Clear()
    {
        Registry.Clear();
    }

    /// <summary>
    /// Registers a new error code definition into the registry.
    /// Throws a <see cref="KyrolusErrorCodeRegistryException"/> if the code is invalid or already registered.
    /// </summary>
    /// <param name="definition">The error code definition to register.</param>
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

    /// <summary>
    /// Registers a collection of error code definitions.
    /// </summary>
    /// <param name="definitions">The collection of definitions to register.</param>
    public static void RegisterRange(IEnumerable<KyrolusErrorCodeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions)
            Register(definition);
    }

    /// <summary>
    /// Registers a new error code definition or updates an existing one (allowing overrides of built-in defaults).
    /// </summary>
    /// <param name="definition">The error code definition to register or update.</param>
    public static void RegisterOrUpdate(KyrolusErrorCodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Code))
            throw new KyrolusErrorCodeRegistryException("Error code cannot be empty.");

        if (!IsValidCode(definition.Code))
            throw new KyrolusErrorCodeRegistryException($"Error code '{definition.Code}' does not match the naming convention.");

        Registry[definition.Code] = definition;
    }

    /// <summary>
    /// Registers or updates a collection of error code definitions.
    /// </summary>
    /// <param name="definitions">The collection of definitions to register or update.</param>
    public static void RegisterOrUpdateRange(IEnumerable<KyrolusErrorCodeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions)
            RegisterOrUpdate(definition);
    }

    /// <summary>
    /// Attempts to retrieve the definition for the specified error code.
    /// Never throws an exception; returns <c>false</c> if the code is not registered.
    /// </summary>
    /// <param name="code">The error code to look up.</param>
    /// <param name="definition">When this method returns <c>true</c>, contains the found definition; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the error code is registered; otherwise, <c>false</c>.</returns>
    public static bool TryGet(string code, out KyrolusErrorCodeDefinition definition)
        => Registry.TryGetValue(code, out definition!);

    /// <summary>
    /// Retrieves the definition for the specified error code, or throws a <see cref="KyrolusErrorCodeRegistryException"/> if not registered.
    /// </summary>
    /// <param name="code">The error code to look up.</param>
    /// <returns>The registered <see cref="KyrolusErrorCodeDefinition"/>.</returns>
    public static KyrolusErrorCodeDefinition Get(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (!Registry.TryGetValue(code, out var definition))
            throw new KyrolusErrorCodeRegistryException($"Error code '{code}' is not registered in KyrolusErrorCodeRegistry.");
        return definition;
    }

    /// <summary>
    /// Returns a point-in-time immutable snapshot collection of all currently registered error code definitions.
    /// </summary>
    /// <returns>A read-only collection containing a copy of all registered definitions.</returns>
    public static IReadOnlyCollection<KyrolusErrorCodeDefinition> Snapshot()
        => [.. Registry.Values];

    /// <summary>
    /// Checks whether an error code string matches the configured naming convention pattern.
    /// </summary>
    /// <param name="code">The error code string to test.</param>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
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
            new KyrolusErrorCodeDefinition(KyrolusErrorCodes.RateLimit, "Rate limit exceeded", HttpStatusCode.TooManyRequests, IsTransient: true, ShouldLog: false),
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
