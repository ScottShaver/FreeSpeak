using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// System moderator and administrator operations on group content reports across all groups. System roles act
    /// as a backup for group moderators. Authorization is enforced inside every method.
    /// </summary>
    public interface ISystemGroupReportService
    {
        /// <summary>
        /// Gets the group report counts for the dashboard overview.
        /// </summary>
        /// <param name="userId">The requesting user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The counts, or null when the user is not a system moderator or administrator.</returns>
        Task<ModerationOverview?> GetOverviewAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the groups that currently have pending reports for the group filter.
        /// </summary>
        /// <param name="userId">The requesting user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The groups, or an empty list when the user is not authorized.</returns>
        Task<IReadOnlyList<GroupReportGroupOption>> GetGroupOptionsAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets one page of the pending group report queue across all groups, oldest first by default.
        /// </summary>
        /// <param name="userId">The requesting user's ID.</param>
        /// <param name="filter">The filters and ordering.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The page; empty when the user is not authorized.</returns>
        Task<GroupReportQueuePage> GetQueueAsync(string userId, GroupReportQueueFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the full detail of one group report.
        /// </summary>
        /// <param name="userId">The requesting user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The detail, or null when the report does not exist or the user is not authorized.</returns>
        Task<GroupReportDetail?> GetDetailAsync(string userId, int reportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a pending report as under review.
        /// </summary>
        /// <param name="userId">The acting user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> MarkUnderReviewAsync(string userId, int reportId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dismisses a report (and every other open report on the same content).
        /// </summary>
        /// <param name="userId">The acting user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional moderator notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> DismissAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves a report without removing content (and every other open report on the same content).
        /// </summary>
        /// <param name="userId">The acting user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional moderator notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> ResolveAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the reported group post or comment and closes the report as action taken.
        /// </summary>
        /// <param name="userId">The acting user's ID.</param>
        /// <param name="reportId">The report ID.</param>
        /// <param name="notes">Optional moderator notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<FeedReportResult> RemoveContentAsync(string userId, int reportId, string? notes, CancellationToken cancellationToken = default);
    }
}
