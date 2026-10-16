using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// Reads and exports the system moderator activity log (the SystemModerator* audit categories).
    /// Every method enforces the system moderator or administrator role itself.
    /// </summary>
    public interface ISystemModeratorActivityService
    {
        /// <summary>
        /// Gets a page of moderator activity, newest first.
        /// </summary>
        /// <param name="requestingUserId">The user asking for the log.</param>
        /// <param name="filter">Filters to apply.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Page size (capped).</param>
        /// <returns>The page, or an unauthorized empty page.</returns>
        Task<ModeratorActivityPage> GetActivityAsync(string requestingUserId, ModeratorActivityFilter filter, int page, int pageSize);

        /// <summary>
        /// Gets the moderators that have recorded activity, for the filter drop-down.
        /// </summary>
        /// <param name="requestingUserId">The user asking.</param>
        /// <returns>The moderators, or an empty list when the user is not authorized.</returns>
        Task<IReadOnlyList<ModeratorOption>> GetModeratorOptionsAsync(string requestingUserId);

        /// <summary>
        /// Exports the filtered activity as CSV (capped) and records the export in the audit log.
        /// </summary>
        /// <param name="requestingUserId">The user exporting.</param>
        /// <param name="filter">Filters to apply.</param>
        /// <returns>The export result.</returns>
        Task<ModeratorActivityExportResult> ExportCsvAsync(string requestingUserId, ModeratorActivityFilter filter);
    }
}
