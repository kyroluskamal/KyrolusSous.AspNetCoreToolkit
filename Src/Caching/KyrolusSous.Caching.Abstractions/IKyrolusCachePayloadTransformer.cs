namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Intercepts serialized byte arrays before storage in cache and restores them upon reading.
/// </summary>
/// <remarks>
/// <b>Real-World Use Cases:</b>
/// <list type="bullet">
///   <item><description><b>Cost &amp; RAM Optimization (Compression):</b> Compressing a 2 MB list of cached cities down to 150 KB using Brotli or Zstd to reduce Redis memory costs and network latency.</description></item>
///   <item><description><b>Regulatory Compliance &amp; Security (Encryption):</b> Encrypting cached user payment profiles with AES-256 before storing them in Redis so that database administrators or infrastructure operators cannot view sensitive plaintext data.</description></item>
/// </list>
/// </remarks>
public interface IKyrolusCachePayloadTransformer
{
    /// <summary>
    /// Transforms the raw serialized byte array before saving to cache (e.g. compresses or encrypts).
    /// </summary>
    /// <param name="payload">The original serialized byte array.</param>
    /// <returns>The transformed byte array to store in cache.</returns>
    byte[] Transform(byte[] payload);

    /// <summary>
    /// Restores the original serialized byte array from the stored cache representation (e.g. decrypts or decompresses).
    /// </summary>
    /// <param name="payload">The transformed byte array read from cache.</param>
    /// <returns>The restored byte array ready for deserialization into a C# object.</returns>
    byte[] Restore(byte[] payload);
}

/// <summary>
/// Extends <see cref="IKyrolusCachePayloadTransformer"/> with an explicit execution order in the transformation pipeline.
/// </summary>
/// <remarks>
/// <b>Why Order Matters in Cryptography &amp; Compression:</b>
/// You must always <b>Compress first, then Encrypt</b> (<c>Compression Order = 10</c>, <c>Encryption Order = 20</c>).
/// Encrypted data appears mathematically random and has maximum entropy, meaning it is impossible to compress. 
/// Compressing before encryption ensures maximum size reduction while keeping the output securely encrypted.
/// </remarks>
public interface IKyrolusOrderedCachePayloadTransformer : IKyrolusCachePayloadTransformer
{
    /// <summary>
    /// Gets the execution priority index in the forward transformation pipeline. Lower numbers run first.
    /// </summary>
    int Order { get; }
}
