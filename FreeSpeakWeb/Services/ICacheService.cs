namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Abstraction for distributed caching operations.
    /// Supports both Redis and in-memory implementations for multi-server deployments.
    /// Provides type-safe caching with automatic JSON serialization.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Gets a value from the cache by key.
        /// Returns default(T) if the key does not exist.
        /// </summary>
        /// <typeparam name="T">The type of the cached value.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The cached value or default(T) if not found.</returns>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in the cache with the specified expiration options.
        /// </summary>
        /// <typeparam name="T">The type of the value to cache.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="options">Cache entry options including expiration settings.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a value from the cache by key.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a value from the cache or creates it using the factory function if not present.
        /// Thread-safe implementation that prevents cache stampede.
        /// </summary>
        /// <typeparam name="T">The type of the cached value.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="factory">Factory function to create the value if not cached.</param>
        /// <param name="options">Cache entry options including expiration settings.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The cached or newly created value.</returns>
        Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes all cache entries matching the specified pattern.
        /// Supports wildcards: * matches any characters, ? matches a single character.
        /// </summary>
        /// <param name="pattern">The pattern to match cache keys against.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The number of entries removed.</returns>
        Task<int> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        /// <param name="key">The cache key to check.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes the sliding expiration for a cache entry.
        /// </summary>
        /// <param name="key">The cache key to refresh.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshAsync(string key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for cache entry expiration and behavior.
    /// </summary>
    public class CacheEntryOptions
    {
        /// <summary>
        /// Gets or sets the absolute expiration time relative to now.
        /// The entry will be removed after this duration regardless of access.
        /// </summary>
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        /// <summary>
        /// Gets or sets the sliding expiration time.
        /// The entry will be removed if not accessed within this duration.
        /// Resets on each access.
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Creates default cache options with 5-minute absolute and 2-minute sliding expiration.
        /// Suitable for frequently accessed data that can be slightly stale.
        /// </summary>
        /// <returns>Default cache entry options.</returns>
        public static CacheEntryOptions Default => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            SlidingExpiration = TimeSpan.FromMinutes(2)
        };

        /// <summary>
        /// Creates short-lived cache options with 1-minute absolute expiration.
        /// Suitable for data that changes frequently.
        /// </summary>
        /// <returns>Short-lived cache entry options.</returns>
        public static CacheEntryOptions ShortLived => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        };

        /// <summary>
        /// Creates long-lived cache options with 30-minute absolute and 10-minute sliding expiration.
        /// Suitable for data that rarely changes.
        /// </summary>
        /// <returns>Long-lived cache entry options.</returns>
        public static CacheEntryOptions LongLived => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            SlidingExpiration = TimeSpan.FromMinutes(10)
        };
    }
}
