using System.Buffers.Binary;
using System.Security.Cryptography;
using KyrolusSous.Auth.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Auth.Runtime;

/// <summary>
/// The default password hasher: PBKDF2 with a per-password random salt, in the same wire format
/// ASP.NET Core Identity v3 uses.
/// </summary>
/// <remarks>
/// Sharing Identity's format is deliberate. An application migrating off
/// <c>Microsoft.AspNetCore.Identity</c> keeps every existing password working, and one migrating
/// back is not trapped either. Legacy Identity v2 hashes (the <c>0x00</c> marker) verify as well,
/// and report <see cref="KyrolusPasswordVerificationResult.SuccessRehashNeeded"/> so they get
/// upgraded on the next successful sign-in.
/// <para>
/// Format, all integers big-endian:
/// <c>[0x01][prf:4][iterations:4][saltLength:4][salt][subkey]</c>.
/// </para>
/// </remarks>
public sealed class KyrolusPbkdf2PasswordHasher : IKyrolusPasswordHasher
{
    private const byte FormatMarkerV2 = 0x00;
    private const byte FormatMarkerV3 = 0x01;

    private const int V2SaltLength = 16;
    private const int V2SubkeyLength = 32;
    private const int V2IterationCount = 1000;

    private const uint PrfHmacSha1 = 0;
    private const uint PrfHmacSha256 = 1;
    private const uint PrfHmacSha512 = 2;

    private readonly KyrolusAuthOptions _options;

    /// <summary>
    /// Initializes a new instance using the configured hashing parameters.
    /// </summary>
    /// <param name="options">The auth runtime options.</param>
    public KyrolusPbkdf2PasswordHasher(IOptions<KyrolusAuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;

        if (_options.Pbkdf2Iterations < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.Pbkdf2Iterations,
                $"{nameof(KyrolusAuthOptions.Pbkdf2Iterations)} must be at least 1.");
        }

        if (_options.SaltSizeInBytes < 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.SaltSizeInBytes,
                $"{nameof(KyrolusAuthOptions.SaltSizeInBytes)} must be at least 8.");
        }

        if (_options.KeySizeInBytes < 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.KeySizeInBytes,
                $"{nameof(KyrolusAuthOptions.KeySizeInBytes)} must be at least 16.");
        }
    }

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        // Resolved first: an algorithm the stored format cannot name must fail with that
        // explanation, not with whatever the KDF happens to say about it.
        var prf = ToPrf(_options.Pbkdf2HashAlgorithm);

        var salt = RandomNumberGenerator.GetBytes(_options.SaltSizeInBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _options.Pbkdf2Iterations,
            _options.Pbkdf2HashAlgorithm,
            _options.KeySizeInBytes);

        var output = new byte[13 + salt.Length + subkey.Length];
        output[0] = FormatMarkerV3;
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(1, 4), prf);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(5, 4), (uint)_options.Pbkdf2Iterations);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(9, 4), (uint)salt.Length);
        salt.CopyTo(output.AsSpan(13));
        subkey.CopyTo(output.AsSpan(13 + salt.Length));

        return Convert.ToBase64String(output);
    }

    /// <inheritdoc />
    public KyrolusPasswordVerificationResult Verify(string hashedPassword, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);

        if (providedPassword.Length > 4096)
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        // A malformed stored hash is a failed verification, not an exception: it is attacker-
        // influenced data in the sense that it decides which code path runs, and a thrown
        // exception here turns into a 500 that distinguishes "corrupt record" from "wrong password".
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(hashedPassword);
        }
        catch (FormatException)
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        if (decoded.Length == 0)
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        return decoded[0] switch
        {
            FormatMarkerV2 => VerifyV2(decoded, providedPassword),
            FormatMarkerV3 => VerifyV3(decoded, providedPassword),
            _ => KyrolusPasswordVerificationResult.Failed,
        };
    }

    private static KyrolusPasswordVerificationResult VerifyV2(byte[] decoded, string providedPassword)
    {
        if (decoded.Length != 1 + V2SaltLength + V2SubkeyLength)
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        var salt = decoded.AsSpan(1, V2SaltLength).ToArray();
        var expected = decoded.AsSpan(1 + V2SaltLength, V2SubkeyLength);

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            providedPassword, salt, V2IterationCount, HashAlgorithmName.SHA1, V2SubkeyLength);

        // Always a rehash: 1,000 iterations of PBKDF2-HMAC-SHA1 is far below anything defensible today.
        return CryptographicOperations.FixedTimeEquals(actual, expected)
            ? KyrolusPasswordVerificationResult.SuccessRehashNeeded
            : KyrolusPasswordVerificationResult.Failed;
    }

    private KyrolusPasswordVerificationResult VerifyV3(byte[] decoded, string providedPassword)
    {
        if (decoded.Length < 13)
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        var prf = BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(1, 4));
        var iterations = BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(5, 4));
        var saltLength = BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(9, 4));

        if (saltLength < 8 || iterations == 0 || iterations > 1_000_000)
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        // Guard the arithmetic before it is used as a length: a crafted saltLength near uint.MaxValue
        // would otherwise overflow into a plausible-looking subkey length.
        var headerAndSalt = 13L + saltLength;
        if (headerAndSalt >= decoded.Length)
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        var subkeyLength = decoded.Length - (int)headerAndSalt;
        if (subkeyLength < 16)
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        if (!TryGetHashAlgorithm(prf, out var algorithm))
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        var salt = decoded.AsSpan(13, (int)saltLength).ToArray();
        var expected = decoded.AsSpan((int)headerAndSalt, subkeyLength);

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            providedPassword, salt, (int)iterations, algorithm, subkeyLength);

        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return KyrolusPasswordVerificationResult.Failed;
        }

        var isCurrent = iterations >= (uint)_options.Pbkdf2Iterations
                        && algorithm == _options.Pbkdf2HashAlgorithm
                        && saltLength >= (uint)_options.SaltSizeInBytes
                        && subkeyLength >= _options.KeySizeInBytes;

        return isCurrent
            ? KyrolusPasswordVerificationResult.Success
            : KyrolusPasswordVerificationResult.SuccessRehashNeeded;
    }

    private static bool TryGetHashAlgorithm(uint prf, out HashAlgorithmName algorithm)
    {
        switch (prf)
        {
            case PrfHmacSha1:
                algorithm = HashAlgorithmName.SHA1;
                return true;
            case PrfHmacSha256:
                algorithm = HashAlgorithmName.SHA256;
                return true;
            case PrfHmacSha512:
                algorithm = HashAlgorithmName.SHA512;
                return true;
            default:
                algorithm = default;
                return false;
        }
    }

    private static uint ToPrf(HashAlgorithmName algorithm)
    {
        if (algorithm == HashAlgorithmName.SHA512)
        {
            return PrfHmacSha512;
        }

        if (algorithm == HashAlgorithmName.SHA256)
        {
            return PrfHmacSha256;
        }

        if (algorithm == HashAlgorithmName.SHA1)
        {
            return PrfHmacSha1;
        }

        throw new NotSupportedException(
            $"The Identity v3 hash format cannot represent {algorithm.Name}. " +
            "Use SHA1, SHA256 or SHA512, or plug in a different IKyrolusPasswordHasher.");
    }
}
