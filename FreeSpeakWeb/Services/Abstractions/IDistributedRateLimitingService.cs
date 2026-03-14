namespace FreeSpeakWeb.Services.Abstractions;

/// <summary>
/// Interface for distributed rate limiting operations.
/// Supports both Redis-based (multi-server) and in-memory (single-server) implementations.
/// </summary>
public interface IDistributedRateLimitingService
{
    /// <summary>
    /// Checks if a request should be allowed based on rate limiting rules.
    /// Automatically increments the counter if the request is allowed.
    /// </summary>
    /// <param name="key">Unique identifier for the rate limit (e.g., userId, IP address, or composite key).</param>
    /// <param name="limitName">Name of the rate limit policy (e.g., "global", "file-download", "login").</param>
    /// <param name="maxRequests">Maximum number of requests allowed in the time window.</param>
    /// <param name="window">Time window for the rate limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing: (IsAllowed, CurrentCount, RetryAfter in seconds if not allowed).</returns>
    Task<(bool IsAllowed, int CurrentCount, int? RetryAfterSeconds)> CheckRateLimitAsync(
        string key,
        string limitName,
        int maxRequests,
        TimeSpan window,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current request count for a specific key without incrementing.
    /// </summary>
    /// <param name="key">Unique identifier for the rate limit.</param>
    /// <param name="limitName">Name of the rate limit policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current request count, or 0 if no requests have been made.</returns>
    Task<int> GetCurrentCountAsync(
        string key,
        string limitName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the rate limit counter for a specific key.
    /// Useful for administrative purposes or after successful authentication.
    /// </summary>
    /// <param name="key">Unique identifier for the rate limit.</param>
    /// <param name="limitName">Name of the rate limit policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the reset was successful, false otherwise.</returns>
    Task<bool> ResetRateLimitAsync(
        string key,
        string limitName,
        CancellationToken cancellationToken = default);
}
