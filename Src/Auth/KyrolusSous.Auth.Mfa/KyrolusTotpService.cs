using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KyrolusSous.Auth.Mfa;

/// <summary>
/// High-performance, AOT-friendly RFC 6238 TOTP service implementation.
/// </summary>
public sealed class KyrolusTotpService : IKyrolusTotpService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private static readonly int Modulo = (int)Math.Pow(10, Digits);

    public string GenerateSecret(int byteLength = 20)
    {
        if (byteLength < 10)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), "Secret must be at least 10 bytes (80 bits).");
        }

        var buffer = new byte[byteLength];
        RandomNumberGenerator.Fill(buffer);
        return ToBase32String(buffer);
    }

    public string GenerateCode(string base32Secret, DateTimeOffset? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);

        var keyBytes = FromBase32String(base32Secret);
        if (keyBytes.Length < 10)
        {
            throw new ArgumentException("The shared secret must be at least 10 bytes (80 bits) to guarantee sufficient cryptographic entropy.", nameof(base32Secret));
        }

        var time = timestamp ?? DateTimeOffset.UtcNow;
        var step = (ulong)(time.ToUnixTimeSeconds() / StepSeconds);

        return ComputeTotp(keyBytes, step);
    }

    public bool ValidateCode(string base32Secret, string code, int allowedClockDriftWindows = 1, DateTimeOffset? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalizedCode = code.Trim().Replace(" ", "").Replace("-", "");
        if (normalizedCode.Length != Digits)
        {
            return false;
        }

        byte[] keyBytes;
        try
        {
            keyBytes = FromBase32String(base32Secret);
            if (keyBytes.Length < 10)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        var time = timestamp ?? DateTimeOffset.UtcNow;
        var currentStep = (ulong)(time.ToUnixTimeSeconds() / StepSeconds);
        var codeBytes = Encoding.UTF8.GetBytes(normalizedCode);
        var drift = Math.Clamp(allowedClockDriftWindows, 0, 10);

        for (var window = -drift; window <= drift; window++)
        {
            var testStep = (ulong)((long)currentStep + window);
            var expectedCode = ComputeTotp(keyBytes, testStep);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedCode);

            if (CryptographicOperations.FixedTimeEquals(codeBytes, expectedBytes))
            {
                return true;
            }
        }

        return false;
    }

    public string GenerateQrCodeUri(string base32Secret, string accountEmail, string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountEmail);
        var cleanSecret = base32Secret.Replace(" ", "").Trim().ToUpperInvariant();

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={cleanSecret}&issuer={encodedIssuer}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    private static string ComputeTotp(byte[] key, ulong step)
    {
        Span<byte> stepBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(stepBytes, step);

        using var hmac = new HMACSHA1(key);
        Span<byte> hash = stackalloc byte[hmac.HashSize / 8];
        hmac.TryComputeHash(stepBytes, hash, out _);

        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        var otp = binaryCode % Modulo;
        return otp.ToString("D6");
    }

    private static string ToBase32String(byte[] bytes)
    {
        var sb = new StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            buffer <<= (5 - bitsLeft);
            sb.Append(Base32Alphabet[buffer & 0x1F]);
        }

        return sb.ToString();
    }

    private static byte[] FromBase32String(string base32)
    {
        var clean = base32.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "").TrimEnd('=');
        var output = new List<byte>((clean.Length * 5) / 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in clean)
        {
            var val = Base32Alphabet.IndexOf(c);
            if (val < 0)
            {
                throw new FormatException($"Invalid Base32 character '{c}'.");
            }

            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return [.. output];
    }
}
