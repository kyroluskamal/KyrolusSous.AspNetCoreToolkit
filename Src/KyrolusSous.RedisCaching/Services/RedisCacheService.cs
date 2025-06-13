
using KyrolusSous.RedisCaching.Services;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace InfrastructureServices.Services.Cache
{
    public class RedisCacheService(IDistributedCache cache, IConnectionMultiplexer redisConnection) : ICacheService
    {
        private readonly IDistributedCache _cache = cache;
        private readonly IDatabase _redisDatabase = redisConnection.GetDatabase();

        public async Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (string.IsNullOrEmpty(cachedData))
            {
                return default;
            }

            return JsonConvert.DeserializeObject<T>(cachedData);
        }


        public async Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
        {
            if (expirationTime == default)
            {
                expirationTime = TimeSpan.FromMinutes(30);
            }
            var serializedData = JsonConvert.SerializeObject(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expirationTime
            };

            await _cache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);
        }

        public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }

        public async Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            return !string.IsNullOrEmpty(cachedData);
        }

        public async Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
        {
            var server = _redisDatabase.Multiplexer.GetServer(_redisDatabase.Multiplexer.GetEndPoints()[0], cancellationToken);

            foreach (var key in server.Keys(pattern: $"*{keyPattern}*"))
            {
                await _redisDatabase.KeyDeleteAsync(key);
            }
        }
    }
}
