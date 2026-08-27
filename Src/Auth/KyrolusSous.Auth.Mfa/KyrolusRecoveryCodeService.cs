using System.Security.Cryptography;
using System.Text;

namespace KyrolusSous.Auth.Mfa;

/// <summary>
/// Service implementation for managing single-use recovery codes.
/// </summary>
public sealed class KyrolusRecoveryCodeService : IKyrolusRecoveryCodeService
{
    private const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // Unambiguous characters (no 0, 1, I, O)

    public IReadOnlyList<string> GenerateRecoveryCodes(int count = 10, int length = 10)
    {
        count = Math.Clamp(count, 1, 100);
        length = Math.Clamp(length, 6, 64);

        var codes = new List<string>(count);
        var bytes = new byte[length];

        for (var i = 0; i < count; i++)
        {
            RandomNumberGenerator.Fill(bytes);
            var sb = new StringBuilder(length);
            for (var j = 0; j < length; j++)
            {
                sb.Append(CodeAlphabet[bytes[j] % CodeAlphabet.Length]);
            }
            codes.Add(sb.ToString());
        }

        return codes;
    }

    public string HashRecoveryCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalized = code.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public bool VerifyRecoveryCode(string rawCode, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(rawCode) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var candidateHash = HashRecoveryCode(rawCode);
        var candidateBytes = Encoding.UTF8.GetBytes(candidateHash);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash.Trim().ToUpperInvariant());

        return CryptographicOperations.FixedTimeEquals(candidateBytes, storedBytes);
    }
}
