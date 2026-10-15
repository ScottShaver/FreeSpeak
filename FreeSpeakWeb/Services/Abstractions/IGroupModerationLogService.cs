using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// The result of a CSV export of a group's moderation log.
    /// </summary>
    /// <param name="IsAuthorized">False when the requester may not export the log.</param>
    /// <param name="Csv">The CSV text (empty when not authorized).</param>
    /// <param name="RowCount">Number of data rows exported.</param>
    /// <param name="Truncated">True when more rows matched than the export cap allows.</param>
    public sealed record GroupModerationLogExportResult(bool IsAuthorized, string Csv, int RowCount, bool Truncated);

    /// <summary>
    /// Records and reads the per-group moderation log.
    /// </summary>
    public interface IGroupModerationLogService
    {
        /// <summary>
        /// Records a moderation action with a server-set UTC timestamp. Never throws; failures are logged.
        /// </summary>
        /// <param name="request">The action to record.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LogAsync(GroupModerationLogRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a page of the log. Only group moderators, group admins and system administrators are authorized.
        /// </summary>
        /// <param name="groupId">The group id.</param>
        /// <param name="requestingUserId">The user asking for the log.</param>
        /// <param name="filter">Filters to apply.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Page size (capped).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The page, or an unauthorized empty page.</returns>
        Task<GroupModerationLogPage> GetLogAsync(
            int groupId,
            string requestingUserId,
            GroupModerationLogFilter filter,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports the filtered log as CSV and records the export in the audit log.
        /// Same authorization as <see cref="GetLogAsync"/>.
        /// </summary>
        /// <param name="groupId">The group id.</param>
        /// <param name="requestingUserId">The user exporting.</param>
        /// <param name="filter">Filters to apply.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The export result.</returns>
        Task<GroupModerationLogExportResult> ExportCsvAsync(
            int groupId,
            string requestingUserId,
            GroupModerationLogFilter filter,
            CancellationToken cancellationToken = default);
    }
}
