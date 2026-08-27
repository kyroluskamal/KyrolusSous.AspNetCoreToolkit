using System.Text;
using Microsoft.Extensions.FileProviders;

namespace KyrolusSous.Auth.Apple;

/// <summary>
/// Supplies the Apple <c>.p8</c> signing key to the client-secret generator from either a file on
/// disk or an in-memory PEM string, behind the single <see cref="IFileInfo"/> shape the Apple
/// handler asks for.
/// </summary>
/// <remarks>
/// The key is read lazily, once per client-secret regeneration (roughly every
/// <see cref="KyrolusAppleAuthOptions.ClientSecretExpiresAfter"/>), so a rotated key file is
/// picked up without a restart.
/// </remarks>
internal sealed class KyrolusApplePrivateKeyFile : IFileInfo
{
    private readonly string? _path;
    private readonly byte[]? _contents;

    private KyrolusApplePrivateKeyFile(string? path, byte[]? contents)
    {
        _path = path;
        _contents = contents;
    }

    /// <summary>Creates a key source that reads from <paramref name="path"/> on demand.</summary>
    public static KyrolusApplePrivateKeyFile FromPath(string path) => new(path, null);

    /// <summary>Creates a key source over an in-memory PEM string.</summary>
    public static KyrolusApplePrivateKeyFile FromPem(string pem) => new(null, Encoding.UTF8.GetBytes(pem));

    public bool Exists => _contents is not null || (_path is not null && File.Exists(_path));

    public bool IsDirectory => false;

    public DateTimeOffset LastModified => _path is not null && File.Exists(_path)
        ? File.GetLastWriteTimeUtc(_path)
        : DateTimeOffset.UnixEpoch;

    public long Length => _contents?.Length ?? (_path is not null && File.Exists(_path)
        ? new FileInfo(_path).Length
        : -1);

    public string Name => _path is not null ? Path.GetFileName(_path) : "apple-authkey.p8";

    public string? PhysicalPath => _path;

    public Stream CreateReadStream()
    {
        if (_contents is not null)
        {
            return new MemoryStream(_contents, writable: false);
        }

        if (_path is null)
        {
            throw new InvalidOperationException("No Apple private key source was configured.");
        }

        return new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}
