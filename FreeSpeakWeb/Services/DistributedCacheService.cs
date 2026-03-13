using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Text.Json;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Distributed cache service implementation supporting Redis or in-memory fallback.
    /// Provides thread-safe caching with automatic JSON serialization and cache stampede prevention.
    /// Designed for multi-server deployments with Redis, with seamless fallback to memory cache.
    /// </summary>
    public class DistributedCacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<DistributedCacheService> _logger;
        private readonly bool _useDistributedCache;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedCacheService"/> class.
        /// </summary>
        /// <param name="distributedCache">The distributed cache implementation (Redis or memory).</param>
        /// <param name="memoryCache">The memory cache for local caching and fallback.</param>
        /// <param name="logger">Logger for recording cache operations.</param>
        /// <param name="configuration">Application configuration for cache settings.</param>
        public DistributedCacheService(
            IDistributedCache distributedCache,
            IMemoryCache memoryCache,
            ILogger<DistributedCacheService> logger,
            IConfiguration configuration)
        {
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;

            // Check if Redis is configured via Caching:UseRedis setting
            _useDistributedCache = configuration.GetValue<bool>("Caching:UseRedis");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            _logger.LogInformation(
                "DistributedCacheService initialized. Using distributed cache (Redis): {UseDistributed}",
                _useDistributedCache);
        }

        /// <summary>
        /// Gets a value from the cache by key.
        /// Returns default(T) if the key does not exist.
        /// </summary>
        /// <typeparam name="T">The type of the cached value.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The cached value or default(T) if not found.</returns>
        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_useDistributedCache)
                {
                    var data = await _distributedCache.GetStringAsync(key, cancellationToken);
                    if (data == null)
                    {
                        _logger.LogDebug("Cache miss for key: {Key}", key);
                        return default;
                    }

                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return JsonSerializer.Deserialize<T>(data, _jsonOptions);
                }
                else
                {
                    if (_memoryCache.TryGetValue(key, out T? value))
                    {
                        _logger.LogDebug("Memory cache hit for key: {Key}", key);
                        return value;
                    }

                    _logger.LogDebug("Memory cache miss for key: {Key}", key);
                    return default;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving cached value for key: {Key}", key);
                return default;
            }
        }

        /// <summary>
        /// Sets a value in the cache with the specified expiration options.
        /// </summary>
        /// <typeparam name="T">The type of the value to cache.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="options">Cache entry options including expiration settings.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= CacheEntryOptions.Default;

            try
            {
                if (_useDistributedCache)
                {
                    var distributedOptions = new DistributedCacheEntryOptions();

                    if (options.AbsoluteExpirationRelativeToNow.HasValue)
                        distributedOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;

                    if (options.SlidingExpiration.HasValue)
                        distributedOptions.SlidingExpiration = options.SlidingExpiration;

                    var data = JsonSerializer.Serialize(value, _jsonOptions);
                    await _distributedCache.SetStringAsync(key, data, distributedOptions, cancellationToken);

                    _logger.LogDebug("Cached value for key: {Key}", key);
                }
                else
                {
                    var memoryOptions = new MemoryCacheEntryOptions();

                    if (options.AbsoluteExpirationRelativeToNow.HasValue)
                        memoryOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;

                    if (options.SlidingExpiration.HasValue)
                        memoryOptions.SlidingExpiration = options.SlidingExpiration;

                    _memoryCache.Set(key, value, memoryOptions);

                    _logger.LogDebug("Memory cached value for key: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching value for key: {Key}", key);
            }
        }

        /// <summary>
        /// Removes a value from the cache by key.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_useDistributedCache)
                {
                    await _distributedCache.RemoveAsync(key, cancellationToken);
                }
                else
                {
                    _memoryCache.Remove(key);
                }

                _logger.LogDebug("Removed cached value for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error removing cached value for key: {Key}", key);
            }
        }

        /// <summary>
        /// Gets a value from the cache or creates it using the factory function if not present.
        /// Thread-safe implementation that prevents cache stampede using per-key locking.
        /// </summary>
        /// <typeparam name="T">The type of the cached value.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="factory">Factory function to create the value if not cached.</param>
        /// <param name="options">Cache entry options including expiration settings.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The cached or newly created value.</returns>
        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        {
            // Try to get from cache first
            var cachedValue = await GetAsync<T>(key, cancellationToken);
            if (cachedValue != null)
            {
                return cachedValue;
            }

            // Use per-key locking to prevent cache stampede
            var lockObj = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await lockObj.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                cachedValue = await GetAsync<T>(key, cancellationToken);
                if (cachedValue != null)
                {
                    return cachedValue;
                }

                // Create the value
                var value = await factory();

                // Cache the value
                await SetAsync(key, value, options, cancellationToken);

                return value;
            }
            finally
            {
                lockObj.Release();

                // Clean up lock if no longer needed
                if (lockObj.CurrentCount == 1)
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Removes all cache entries matching the specified pattern.
        /// Note: Pattern matching is only fully supported with Redis.
        /// For memory cache, this operation is not supported and returns 0.
        /// </summary>
        /// <param name="pattern">The pattern to match cache keys against.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The number of entries removed (always 0 for memory cache).</returns>
        public async Task<int> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            // Pattern removal is only fully supported with Redis
            // For memory cache, we log a warning as this operation isn't supported
            if (!_useDistributedCache)
            {
                _logger.LogWarning(
                    "RemoveByPatternAsync is not fully supported with memory cache. Pattern: {Pattern}",
                    pattern);
                return 0;
            }

            // Note: IDistributedCache doesn't support pattern-based removal directly.
            // This would require direct Redis access via StackExchange.Redis.
            // For now, we log and return 0. Full implementation would use:
            // var server = redis.GetServer(endpoint);
            // var keys = server.Keys(pattern: pattern);
            // foreach (var key in keys) await RemoveAsync(key.ToString());

            _logger.LogDebug(
                "RemoveByPatternAsync called with pattern: {Pattern}. " +
                "Direct Redis access required for full implementation.",
                pattern);

            return await Task.FromResult(0);
        }

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        /// <param name="key">The cache key to check.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_useDistributedCache)
                {
                    var data = await _distributedCache.GetStringAsync(key, cancellationToken);
                    return data != null;
                }
                else
                {
                    return _memoryCache.TryGetValue(key, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking cache existence for key: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Refreshes the sliding expiration for a cache entry.
        /// </summary>
        /// <param name="key">The cache key to refresh.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_useDistributedCache)
                {
                    await _distributedCache.RefreshAsync(key, cancellationToken);
                    _logger.LogDebug("Refreshed cache entry for key: {Key}", key);
                }
                // Memory cache doesn't support explicit refresh - access automatically refreshes sliding expiration
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error refreshing cache entry for key: {Key}", key);
            }
        }
    }
}
