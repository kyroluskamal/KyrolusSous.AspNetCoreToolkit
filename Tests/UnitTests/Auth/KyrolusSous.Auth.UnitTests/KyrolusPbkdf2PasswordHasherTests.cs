using System.Buffers.Binary;
using System.Security.Cryptography;
using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Runtime;
using Microsoft.Extensions.Options;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

public sealed class KyrolusPbkdf2PasswordHasherTests
{
    // The production default (210,000 SHA-512 iterations) is deliberately slow. Tests that only
    // care about format and control flow use the floor the validator allows instead.
    private static KyrolusPbkdf2PasswordHasher CreateHasher(Action<KyrolusAuthOptions>? configure = null)
    {
        var options = new KyrolusAuthOptions { Pbkdf2Iterations = 10_000 };
        configure?.Invoke(options);
        return new KyrolusPbkdf2PasswordHasher(Options.Create(options));
    }

    [Fact(DisplayName = "Hash then verify succeeds")]
    public void Hash_then_verify_succeeds()
    {
        var hasher = CreateHasher();

        var hash = hasher.Hash("correct horse battery staple");

        hasher.Verify(hash, "correct horse battery staple")
            .ShouldBe(KyrolusPasswordVerificationResult.Success);
    }

    [Fact(DisplayName = "Verify rejects a wrong password")]
    public void Verify_rejects_a_wrong_password()
    {
        var hasher = CreateHasher();

        var hash = hasher.Hash("correct horse battery staple");

        hasher.Verify(hash, "Correct horse battery staple")
            .ShouldBe(KyrolusPasswordVerificationResult.Failed);
    }

    [Fact(DisplayName = "Hashing the same password twice produces different hashes")]
    public void Hashing_the_same_password_twice_produces_different_hashes()
    {
        var hasher = CreateHasher();

        hasher.Hash("same").ShouldNotBe(hasher.Hash("same"));
    }

    [Fact(DisplayName = "Hash uses the configured parameters")]
    public void Hash_uses_the_configured_parameters()
    {
        var hasher = CreateHasher(o =>
        {
            o.Pbkdf2Iterations = 12_345;
            o.SaltSizeInBytes = 24;
            o.KeySizeInBytes = 48;
            o.Pbkdf2HashAlgorithm = HashAlgorithmName.SHA256;
        });

        var decoded = Convert.FromBase64String(hasher.Hash("pw"));

        decoded[0].ShouldBe((byte)0x01);
        BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(1, 4)).ShouldBe(1u); // HMACSHA256
        BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(5, 4)).ShouldBe(12_345u);
        BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(9, 4)).ShouldBe(24u);
        decoded.Length.ShouldBe(13 + 24 + 48);
    }

    [Fact(DisplayName = "Verify asks for a rehash when the stored iteration count is below the configured one")]
    public void Verify_asks_for_a_rehash_when_the_stored_iteration_count_is_below_the_configured_one()
    {
        var weak = CreateHasher(o => o.Pbkdf2Iterations = 10_000);
        var hash = weak.Hash("pw");

        var strong = CreateHasher(o => o.Pbkdf2Iterations = 50_000);

        strong.Verify(hash, "pw").ShouldBe(KyrolusPasswordVerificationResult.SuccessRehashNeeded);
    }

    [Fact(DisplayName = "Verify asks for a rehash when the stored algorithm is weaker than the configured one")]
    public void Verify_asks_for_a_rehash_when_the_stored_algorithm_is_weaker_than_the_configured_one()
    {
        var sha256 = CreateHasher(o => o.Pbkdf2HashAlgorithm = HashAlgorithmName.SHA256);
        var hash = sha256.Hash("pw");

        var sha512 = CreateHasher(o => o.Pbkdf2HashAlgorithm = HashAlgorithmName.SHA512);

        sha512.Verify(hash, "pw").ShouldBe(KyrolusPasswordVerificationResult.SuccessRehashNeeded);
    }

    [Fact(DisplayName = "Verify accepts a legacy identity v2 hash and asks for a rehash")]
    public void Verify_accepts_a_legacy_identity_v2_hash_and_asks_for_a_rehash()
    {
        // The ASP.NET Identity v2 layout: marker 0x00, a 16-byte salt, then 32 bytes of
        // PBKDF2-HMAC-SHA1 over 1,000 iterations.
        var salt = RandomNumberGenerator.GetBytes(16);
        var subkey = Rfc2898DeriveBytes.Pbkdf2("legacy", salt, 1000, HashAlgorithmName.SHA1, 32);

        var stored = new byte[1 + 16 + 32];
        stored[0] = 0x00;
        salt.CopyTo(stored.AsSpan(1));
        subkey.CopyTo(stored.AsSpan(17));

        var hasher = CreateHasher();

        hasher.Verify(Convert.ToBase64String(stored), "legacy")
            .ShouldBe(KyrolusPasswordVerificationResult.SuccessRehashNeeded);
        hasher.Verify(Convert.ToBase64String(stored), "wrong")
            .ShouldBe(KyrolusPasswordVerificationResult.Failed);
    }

    [Fact(DisplayName = "Verify accepts an identity v3 hash produced elsewhere")]
    public void Verify_accepts_an_identity_v3_hash_produced_elsewhere()
    {
        // Identity's own default in recent versions: HMACSHA512, 100,000 iterations, 16-byte salt.
        var salt = RandomNumberGenerator.GetBytes(16);
        var subkey = Rfc2898DeriveBytes.Pbkdf2("external", salt, 100_000, HashAlgorithmName.SHA512, 32);

        var stored = new byte[13 + 16 + 32];
        stored[0] = 0x01;
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(1, 4), 2); // HMACSHA512
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(5, 4), 100_000);
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(9, 4), 16);
        salt.CopyTo(stored.AsSpan(13));
        subkey.CopyTo(stored.AsSpan(29));

        var hasher = CreateHasher(o => o.Pbkdf2Iterations = 100_000);

        hasher.Verify(Convert.ToBase64String(stored), "external")
            .ShouldBe(KyrolusPasswordVerificationResult.Success);
    }

    [Theory(DisplayName = "Verify returns failed for a malformed stored hash")]
    [InlineData("")]
    [InlineData("not base64 at all !!")]
    [InlineData("AA==")]                     // one byte, unknown marker
    [InlineData("AQ==")]                     // v3 marker with no header
    [InlineData("AAAAAAAAAAAAAAAAAAAA")]     // v2 marker, wrong length
    public void Verify_returns_failed_for_a_malformed_stored_hash(string stored)
    {
        var hasher = CreateHasher();

        hasher.Verify(stored, "pw").ShouldBe(KyrolusPasswordVerificationResult.Failed);
    }

    [Fact(DisplayName = "Verify returns failed when the stored hash names an unknown prf")]
    public void Verify_returns_failed_when_the_stored_hash_names_an_unknown_prf()
    {
        var stored = new byte[13 + 16 + 32];
        stored[0] = 0x01;
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(1, 4), 99); // no such PRF
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(5, 4), 10_000);
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(9, 4), 16);

        CreateHasher().Verify(Convert.ToBase64String(stored), "pw")
            .ShouldBe(KyrolusPasswordVerificationResult.Failed);
    }

    [Fact(DisplayName = "Verify returns failed when the declared salt length runs past the buffer")]
    public void Verify_returns_failed_when_the_declared_salt_length_runs_past_the_buffer()
    {
        // A crafted length that would overflow into a plausible-looking subkey length if the
        // arithmetic were done in 32 bits.
        var stored = new byte[13 + 16 + 32];
        stored[0] = 0x01;
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(1, 4), 2);
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(5, 4), 10_000);
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(9, 4), uint.MaxValue);

        CreateHasher().Verify(Convert.ToBase64String(stored), "pw")
            .ShouldBe(KyrolusPasswordVerificationResult.Failed);
    }

    [Theory(DisplayName = "Constructor rejects a non positive iteration count")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_a_non_positive_iteration_count(int iterations)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateHasher(o => o.Pbkdf2Iterations = iterations));
    }

    [Fact(DisplayName = "Hash rejects an algorithm the stored format cannot describe")]
    public void Hash_rejects_an_algorithm_the_stored_format_cannot_describe()
    {
        var hasher = CreateHasher(o => o.Pbkdf2HashAlgorithm = HashAlgorithmName.MD5);

        Should.Throw<NotSupportedException>(() => hasher.Hash("pw"));
    }

    [Fact(DisplayName = "Verify rejects excessive iterations without burning cpu")]
    public void Verify_rejects_excessive_iterations_without_burning_cpu()
    {
        // Hash crafted with 2,000,000 iterations (> 1,000,000 max allowed)
        var stored = new byte[13 + 16 + 32];
        stored[0] = 0x01;
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(1, 4), 2); // SHA512
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(5, 4), 2_000_000); // 2M iterations
        BinaryPrimitives.WriteUInt32BigEndian(stored.AsSpan(9, 4), 16); // 16 byte salt

        var result = CreateHasher().Verify(Convert.ToBase64String(stored), "pw");
        result.ShouldBe(KyrolusPasswordVerificationResult.Failed);
    }
}
