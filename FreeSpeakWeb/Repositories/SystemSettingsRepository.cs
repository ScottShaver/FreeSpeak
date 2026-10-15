using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ISystemSettingsRepository"/>.
    /// </summary>
    public class SystemSettingsRepository : ISystemSettingsRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<SystemSettingsRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemSettingsRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository failures.</param>
        public SystemSettingsRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<SystemSettingsRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.SystemSettings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task UpsertManyAsync(IReadOnlyCollection<SystemSetting> settings, CancellationToken cancellationToken = default)
        {
            if (settings.Count == 0)
            {
                return;
            }

            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var keys = settings.Select(s => s.Key).ToList();
                var existing = await context.SystemSettings
                    .Where(s => keys.Contains(s.Key))
                    .ToDictionaryAsync(s => s.Key, cancellationToken);

                foreach (var setting in settings)
                {
                    if (existing.TryGetValue(setting.Key, out var row))
                    {
                        row.Value = setting.Value;
                        row.IsSecret = setting.IsSecret;
                        row.UpdatedAtUtc = setting.UpdatedAtUtc;
                        row.UpdatedByUserId = setting.UpdatedByUserId;
                    }
                    else
                    {
                        context.SystemSettings.Add(setting);
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert {Count} system settings", settings.Count);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
        {
            if (keys.Count == 0)
            {
                return 0;
            }

            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                return await context.SystemSettings
                    .Where(s => keys.Contains(s.Key))
                    .ExecuteDeleteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete {Count} system settings", keys.Count);
                throw;
            }
        }
    }
}
