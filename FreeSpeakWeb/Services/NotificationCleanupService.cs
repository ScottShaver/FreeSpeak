using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Background service that periodically cleans up expired notifications
/// </summary>
public class NotificationCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationCleanupService> _logger;
    private static DateTime _lastCleanupTime = DateTime.MinValue;
    private static readonly SemaphoreSlim _cleanupLock = new(1, 1);
    
    // Minimum time between cleanup runs (1 minute)
    private static readonly TimeSpan MinimumCleanupInterval = TimeSpan.FromMinutes(1);
    
    // How often the background job runs (5 minutes)
    private static readonly TimeSpan BackgroundJobInterval = TimeSpan.FromMinutes(5);

    public NotificationCleanupService(
        IServiceProvider serviceProvider,
        ILogger<NotificationCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Cleanup Service is starting");

        // Wait 1 minute before first run to allow application to fully start
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during notification cleanup");
            }

            // Wait for the next interval
            await Task.Delay(BackgroundJobInterval, stoppingToken);
        }

        _logger.LogInformation("Notification Cleanup Service is stopping");
    }

    /// <summary>
    /// Perform the cleanup with throttling to ensure it doesn't run too frequently
    /// </summary>
    public static async Task<bool> PerformCleanupAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        // Check if enough time has passed since last cleanup (throttling)
        var timeSinceLastCleanup = DateTime.UtcNow - _lastCleanupTime;
        if (timeSinceLastCleanup < MinimumCleanupInterval)
        {
            logger.LogDebug("Skipping notification cleanup - last run was {Seconds} seconds ago (minimum {MinSeconds} seconds)", 
                timeSinceLastCleanup.TotalSeconds, MinimumCleanupInterval.TotalSeconds);
            return false;
        }

        // Use semaphore to ensure only one cleanup runs at a time
        if (!await _cleanupLock.WaitAsync(0))
        {
            logger.LogDebug("Skipping notification cleanup - another cleanup is already in progress");
            return false;
        }

        try
        {
            // Double-check after acquiring lock
            timeSinceLastCleanup = DateTime.UtcNow - _lastCleanupTime;
            if (timeSinceLastCleanup < MinimumCleanupInterval)
            {
                return false;
            }

            using var scope = serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var result = await notificationService.DeleteExpiredNotificationsAsync();
            
            if (result.Success && result.DeletedCount > 0)
            {
                logger.LogInformation("Notification cleanup completed - deleted {Count} expired notifications", result.DeletedCount);
            }
            else if (result.Success)
            {
                logger.LogDebug("Notification cleanup completed - no expired notifications found");
            }
            else
            {
                logger.LogWarning("Notification cleanup failed: {Error}", result.ErrorMessage);
            }

            _lastCleanupTime = DateTime.UtcNow;
            return result.Success;
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    private async Task PerformCleanupAsync()
    {
        await PerformCleanupAsync(_serviceProvider, _logger);
    }

    /// <summary>
    /// Manually trigger cleanup (respects throttling)
    /// </summary>
    public static async Task<bool> TriggerCleanupAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        return await PerformCleanupAsync(serviceProvider, logger);
    }

    /// <summary>
    /// Get the time since last cleanup
    /// </summary>
    public static TimeSpan TimeSinceLastCleanup => DateTime.UtcNow - _lastCleanupTime;

    /// <summary>
    /// Check if cleanup should run based on throttling
    /// </summary>
    public static bool ShouldRunCleanup => DateTime.UtcNow - _lastCleanupTime >= MinimumCleanupInterval;
}
