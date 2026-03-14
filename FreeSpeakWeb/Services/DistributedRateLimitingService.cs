using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Concurrent;
using System.Text.Json;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Distributed rate limiting service that supports both Redis-based (multi-server)
/// and in-memory (single-server) rate limiting.
/// Uses IDistributedCache for storage, which can be backed by Redis or memory cache.
/// </summary>
public class DistributedRateLimitingService : IDistributedRateLimitingService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedRateLimitingService> _logger;
    private readonly bool _useRedis;

    // In-memory fallback for when distributed cache operations fail
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _fallbackCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedRateLimitingService"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache instance (Redis or memory-based).</param>
    /// <param name="configuration">Application configuration for checking Redis settings.</param>
    /// <param name="logger">Logger for recording rate limiting operations.</param>
    public DistributedRateLimitingService(
        IDistributedCache cache,
        IConfiguration configuration,
        ILogger<DistributedRateLimitingService> logger)
    {
        _cache = cache;
        _logger = logger;
        _useRedis = configuration.GetValue<bool>("Caching:UseRedis");
    }

    /// <summary>
    /// Checks if a request should be allowed based on rate limiting rules.
    /// Automatically increments the counter if the request is allowed.
    /// </summary>
    /// <param name="key">Unique identifier for the rate limit (e.g., userId, IP address).</param>
    /// <param name="limitName">Name of the rate limit policy.</param>
    /// <param name="maxRequests">Maximum number of requests allowed in the time window.</param>
    /// <param name="window">Time window for the rate limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing: (IsAllowed, CurrentCount, RetryAfterSeconds).</returns>
    public async Task<(bool IsAllowed, int CurrentCount, int? RetryAfterSeconds)> CheckRateLimitAsync(
        string key,
        string limitName,
        int maxRequests,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(key, limitName);

        try
        {
            var entry = await GetOrCreateEntryAsync(cacheKey, window, cancellationToken);

            // Check if window has expired and reset if needed
            if (entry.WindowStart.Add(window) < DateTime.UtcNow)
            {
                entry = new RateLimitEntry
                {
                    Count = 0,
                    WindowStart = DateTime.UtcNow
                };
            }

            // Increment count
            entry.Count++;

            // Save updated entry
            await SaveEntryAsync(cacheKey, entry, window, cancellationToken);

            if (entry.Count > maxRequests)
            {
                var retryAfter = (int)(entry.WindowStart.Add(window) - DateTime.UtcNow).TotalSeconds;
                retryAfter = Math.Max(1, retryAfter); // At least 1 second

                _logger.LogWarning(
                    "Rate limit exceeded for key {Key} policy {Policy}: {Count}/{Max}",
                    key, limitName, entry.Count, maxRequests);

                return (false, entry.Count, retryAfter);
            }

            return (true, entry.Count, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for key {Key}, falling back to in-memory", key);
            return CheckRateLimitFallback(cacheKey, maxRequests, window);
        }
    }

    /// <summary>
    /// Gets the current request count for a specific key without incrementing.
    /// </summary>
    /// <param name="key">Unique identifier for the rate limit.</param>
    /// <param name="limitName">Name of the rate limit policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current request count, or 0 if no requests have been made.</returns>
    public async Task<int> GetCurrentCountAsync(
        string key,
        string limitName,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(key, limitName);

        try
        {
            var data = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (string.IsNullOrEmpty(data))
            {
                return 0;
            }

            var entry = JsonSerializer.Deserialize<RateLimitEntry>(data);
            return entry?.Count ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limit count for key {Key}", key);

            // Try fallback cache
            if (_fallbackCache.TryGetValue(cacheKey, out var fallbackEntry))
            {
                return fallbackEntry.Count;
            }

            return 0;
        }
    }

    /// <summary>
    /// Resets the rate limit counter for a specific key.
    /// </summary>
    /// <param name="key">Unique identifier for the rate limit.</param>
    /// <param name="limitName">Name of the rate limit policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the reset was successful, false otherwise.</returns>
    public async Task<bool> ResetRateLimitAsync(
        string key,
        string limitName,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(key, limitName);

        try
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            _fallbackCache.TryRemove(cacheKey, out _);

            _logger.LogInformation("Rate limit reset for key {Key} policy {Policy}", key, limitName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting rate limit for key {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Builds a cache key combining the user key and limit policy name.
    /// </summary>
    private static string BuildCacheKey(string key, string limitName)
    {
        return $"ratelimit:{limitName}:{key}";
    }

    /// <summary>
    /// Gets an existing rate limit entry or creates a new one.
    /// </summary>
    private async Task<RateLimitEntry> GetOrCreateEntryAsync(
        string cacheKey,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var data = await _cache.GetStringAsync(cacheKey, cancellationToken);

        if (!string.IsNullOrEmpty(data))
        {
            var entry = JsonSerializer.Deserialize<RateLimitEntry>(data);
            if (entry != null)
            {
                return entry;
            }
        }

        return new RateLimitEntry
        {
            Count = 0,
            WindowStart = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Saves a rate limit entry to the distributed cache.
    /// </summary>
    private async Task SaveEntryAsync(
        string cacheKey,
        RateLimitEntry entry,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(entry);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = window.Add(TimeSpan.FromSeconds(10)) // Add buffer
        };

        await _cache.SetStringAsync(cacheKey, data, options, cancellationToken);

        // Also update fallback cache for resilience
        _fallbackCache[cacheKey] = entry;
    }

    /// <summary>
    /// Fallback rate limiting using in-memory cache when distributed cache fails.
    /// </summary>
    private (bool IsAllowed, int CurrentCount, int? RetryAfterSeconds) CheckRateLimitFallback(
        string cacheKey,
        int maxRequests,
        TimeSpan window)
    {
        var now = DateTime.UtcNow;

        var entry = _fallbackCache.AddOrUpdate(
            cacheKey,
            _ => new RateLimitEntry { Count = 1, WindowStart = now },
            (_, existing) =>
            {
                // Reset if window expired
                if (existing.WindowStart.Add(window) < now)
                {
                    return new RateLimitEntry { Count = 1, WindowStart = now };
                }

                existing.Count++;
                return existing;
            });

        if (entry.Count > maxRequests)
        {
            var retryAfter = (int)(entry.WindowStart.Add(window) - now).TotalSeconds;
            return (false, entry.Count, Math.Max(1, retryAfter));
        }

        return (true, entry.Count, null);
    }

    /// <summary>
    /// Represents a rate limit entry stored in cache.
    /// </summary>
    private class RateLimitEntry
    {
        /// <summary>
        /// Gets or sets the number of requests made in the current window.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the start time of the current rate limit window.
        /// </summary>
        public DateTime WindowStart { get; set; }
    }
}
