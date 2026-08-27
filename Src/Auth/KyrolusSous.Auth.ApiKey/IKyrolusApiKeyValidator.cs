namespace KyrolusSous.Auth.ApiKey;

/// <summary>
/// Storage-agnostic contract for validating an incoming API key.
/// Consumer applications can implement this against any store (EF Core, Marten, Redis, Memory, etc.).
/// </summary>
public interface IKyrolusApiKeyValidator
{
    Task<KyrolusApiKeyValidationResult> ValidateAsync(string providedKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service contract for generating cryptographically secure API keys and computing hashes.
/// </summary>
public interface IKyrolusApiKeyGenerator
{
    string GenerateKey(string prefix = "kyr_");
    string HashKey(string rawKey);
}
