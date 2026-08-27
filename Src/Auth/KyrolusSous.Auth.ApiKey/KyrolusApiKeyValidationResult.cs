namespace KyrolusSous.Auth.ApiKey;

/// <summary>
/// Outcome of validating an API key.
/// </summary>
public sealed class KyrolusApiKeyValidationResult
{
    public bool Succeeded { get; private init; }
    public IKyrolusApiKey? ApiKey { get; private init; }
    public string? FailureReason { get; private init; }

    public static KyrolusApiKeyValidationResult Success(IKyrolusApiKey apiKey)
        => new() { Succeeded = true, ApiKey = apiKey };

    public static KyrolusApiKeyValidationResult Failed(string reason)
        => new() { Succeeded = false, FailureReason = reason };
}
