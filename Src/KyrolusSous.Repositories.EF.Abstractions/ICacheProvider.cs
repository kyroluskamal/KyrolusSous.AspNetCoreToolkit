using System;
using System.Threading;
using System.Threading.Tasks;

namespace KyrolusSous.Repositories.EF.Abstractions;

/// <summary>
/// Simple cache abstraction used by generated repositories. Implement with Redis or in-memory as needed.
/// </summary>
public interface ICacheProvider
{
    Task<T?> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
