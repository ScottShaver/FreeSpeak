using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository for administrator-managed configuration overrides stored in the SystemSettings table.
    /// </summary>
    public interface ISystemSettingsRepository
    {
        /// <summary>
        /// Gets every stored setting.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>All rows, ordered by key.</returns>
        Task<List<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts or updates the given settings in a single transaction.
        /// </summary>
        /// <param name="settings">The rows to upsert, matched on <see cref="SystemSetting.Key"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        Task UpsertManyAsync(IReadOnlyCollection<SystemSetting> settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the rows with the given keys (reverting those settings to the file defaults).
        /// </summary>
        /// <param name="keys">The keys to delete. Missing keys are ignored.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of rows deleted.</returns>
        Task<int> DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
    }
}
