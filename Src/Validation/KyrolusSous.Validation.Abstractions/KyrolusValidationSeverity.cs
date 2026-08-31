namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Specifies the severity level of a validation failure.
/// </summary>
public enum KyrolusValidationSeverity
{
    /// <summary>Informational failure level (useful for non-blocking UI hints or suggestions).</summary>
    Info = 0,

    /// <summary>Warning failure level (highlights potential issues without necessarily preventing operation).</summary>
    Warning = 1,

    /// <summary>Error failure level (blocking failure representing an invalid state).</summary>
    Error = 2
}
