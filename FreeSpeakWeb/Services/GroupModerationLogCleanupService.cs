using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Background service that daily purges group moderation log entries older than the configured retention period.
    /// </summary>
    public class GroupModerationLogCleanupService : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan RunInterval = TimeSpan.FromDays(1);

        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<GroupModerationLogSettings> _settings;
        private readonly ILogger<GroupModerationLogCleanupService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupModerationLogCleanupService"/> class.
        /// </summary>
        /// <param name="serviceProvider">Provider used to create a scope per run.</param>
        /// <param name="settings">Moderation log settings.</param>
        /// <param name="logger">Logger.</param>
        public GroupModerationLogCleanupService(
            IServiceProvider serviceProvider,
            IOptions<GroupModerationLogSettings> settings,
            ILogger<GroupModerationLogCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings;
            _logger = logger;
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Group Moderation Log Cleanup Service is starting");

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await RunCleanupAsync(DateTime.UtcNow, stoppingToken);
                    await Task.Delay(RunInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }

            _logger.LogInformation("Group Moderation Log Cleanup Service is stopping");
        }

        /// <summary>
        /// Deletes entries older than the retention period relative to <paramref name="utcNow"/>.
        /// Errors are logged and never thrown.
        /// </summary>
        /// <param name="utcNow">The current UTC time.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of deleted entries.</returns>
        public async Task<int> RunCleanupAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            try
            {
                var settings = _settings.Value;
                var cutoff = utcNow.AddMonths(-Math.Max(settings.RetentionMonths, 1));

                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IGroupModerationLogRepository>();
                var deleted = await repository.DeleteOlderThanAsync(cutoff, Math.Max(settings.CleanupBatchSize, 1), cancellationToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("Group moderation log cleanup deleted {Count} entries older than {Cutoff:u}", deleted, cutoff);
                }

                return deleted;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during group moderation log cleanup");
                return 0;
            }
        }
    }
}
