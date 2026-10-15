using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Data access for the per-group moderation log.
    /// </summary>
    public interface IGroupModerationLogRepository
    {
        /// <summary>
        /// Persists a moderation log entry.
        /// </summary>
        /// <param name="entry">The entry to save.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddAsync(GroupModerationLogEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a page of a group's entries, newest first.
        /// </summary>
        /// <param name="groupId">The group id.</param>
        /// <param name="filter">Filters to apply.</param>
        /// <param name="pageNumber">1-based page number.</param>
        /// <param name="pageSize">Page size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The entries on the page and the total number of matching entries.</returns>
        Task<(List<GroupModerationLogEntry> Items, int TotalCount)> GetPagedAsync(
            int groupId,
            GroupModerationLogFilter filter,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets up to <paramref name="maxRows"/> matching entries, newest first, for export.
        /// </summary>
        /// <param name="groupId">The group id.</param>
        /// <param name="filter">Filters to apply.</param>
        /// <param name="maxRows">Maximum rows to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching entries.</returns>
        Task<List<GroupModerationLogEntry>> GetForExportAsync(
            int groupId,
            GroupModerationLogFilter filter,
            int maxRows,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves display names for the given user ids. Unknown users are omitted.
        /// </summary>
        /// <param name="userIds">The user ids.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A map of user id to display name.</returns>
        Task<Dictionary<string, string>> GetDisplayNamesAsync(
            IEnumerable<string> userIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes entries created before the cutoff, in batches.
        /// </summary>
        /// <param name="cutoffUtc">Entries older than this UTC time are deleted.</param>
        /// <param name="batchSize">Rows deleted per batch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The total number of deleted rows.</returns>
        Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default);
    }
}
