namespace FreeSpeakWeb;

/// <summary>
/// Configuration settings for rate limiting policies.
/// Allows customization of rate limits via appsettings.json.
/// </summary>
public class RateLimitingSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Gets or sets whether distributed rate limiting is enabled.
    /// When true, uses the distributed cache (Redis if configured) for rate limiting.
    /// When false, uses the built-in ASP.NET Core in-memory rate limiting.
    /// Default: false
    /// </summary>
    public bool UseDistributed { get; set; } = false;

    /// <summary>
    /// Gets or sets the global rate limit settings.
    /// Applies to all requests across the application.
    /// </summary>
    public RateLimitPolicy Global { get; set; } = new()
    {
        MaxRequests = 500,
        WindowMinutes = 1,
        QueueLimit = 20
    };

    /// <summary>
    /// Gets or sets the file download rate limit settings.
    /// Applies to SecureFileController endpoints.
    /// </summary>
    public RateLimitPolicy FileDownload { get; set; } = new()
    {
        MaxRequests = 100,
        WindowMinutes = 1,
        QueueLimit = 10
    };

    /// <summary>
    /// Gets or sets the login attempt rate limit settings.
    /// Applies to authentication endpoints to prevent brute force attacks.
    /// </summary>
    public RateLimitPolicy Login { get; set; } = new()
    {
        MaxRequests = 10,
        WindowMinutes = 5,
        QueueLimit = 0
    };

    /// <summary>
    /// Gets or sets the API endpoint rate limit settings.
    /// Applies to API controllers for general API protection.
    /// </summary>
    public RateLimitPolicy Api { get; set; } = new()
    {
        MaxRequests = 200,
        WindowMinutes = 1,
        QueueLimit = 5
    };
}

/// <summary>
/// Represents a single rate limit policy configuration.
/// </summary>
public class RateLimitPolicy
{
    /// <summary>
    /// Gets or sets the maximum number of requests allowed in the time window.
    /// </summary>
    public int MaxRequests { get; set; }

    /// <summary>
    /// Gets or sets the time window duration in minutes.
    /// </summary>
    public int WindowMinutes { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of requests that can be queued.
    /// Set to 0 to disable queuing (immediately reject over-limit requests).
    /// </summary>
    public int QueueLimit { get; set; }

    /// <summary>
    /// Gets the time window as a TimeSpan.
    /// </summary>
    public TimeSpan Window => TimeSpan.FromMinutes(WindowMinutes);
}
