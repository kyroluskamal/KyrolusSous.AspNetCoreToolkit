using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Auth.Runtime;

/// <summary>
/// Validates <see cref="KyrolusAuthOptions"/> at startup, so a value that would silently weaken
/// password storage fails the build-out instead of shipping.
/// </summary>
public sealed class KyrolusAuthOptionsValidator : IValidateOptions<KyrolusAuthOptions>
{
    /// <summary>The lowest PBKDF2 iteration count this library will accept.</summary>
    public const int MinimumIterations = 10_000;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, KyrolusAuthOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Kyrolus auth options are missing.");
        }

        var failures = new List<string>();

        if (options.Pbkdf2Iterations < MinimumIterations)
        {
            failures.Add(
                $"{nameof(KyrolusAuthOptions.Pbkdf2Iterations)} is {options.Pbkdf2Iterations}; " +
                $"anything below {MinimumIterations} is not defensible against offline cracking.");
        }

        if (options.SaltSizeInBytes < 8)
        {
            failures.Add($"{nameof(KyrolusAuthOptions.SaltSizeInBytes)} must be at least 8.");
        }

        if (options.KeySizeInBytes < 16)
        {
            failures.Add($"{nameof(KyrolusAuthOptions.KeySizeInBytes)} must be at least 16.");
        }

        if (options.Pbkdf2HashAlgorithm != HashAlgorithmName.SHA512 &&
            options.Pbkdf2HashAlgorithm != HashAlgorithmName.SHA256 &&
            options.Pbkdf2HashAlgorithm != HashAlgorithmName.SHA1)
        {
            failures.Add(
                $"{nameof(KyrolusAuthOptions.Pbkdf2HashAlgorithm)} must be SHA1, SHA256 or SHA512; " +
                $"'{options.Pbkdf2HashAlgorithm.Name}' cannot be represented in the stored hash format.");
        }

        if (options.MaxFailedAccessAttempts < 0)
        {
            failures.Add($"{nameof(KyrolusAuthOptions.MaxFailedAccessAttempts)} cannot be negative.");
        }

        if (options.LockoutDuration < TimeSpan.Zero)
        {
            failures.Add($"{nameof(KyrolusAuthOptions.LockoutDuration)} cannot be negative.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
